using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;
using Funforum.Api.Security;
using Funforum.Api.Services;
using Funforum.Api.Tests.Integration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Funforum.Api.Tests.Admin;

// X.2: mỗi hành động ẩn/xóa (và role_change / board_*) tạo đúng 1 dòng audit_logs
// với metadata đủ để khôi phục. Ban/unban chờ X.4.
public sealed class AuditLogWriteTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnectionString;
    private readonly string? _schemaName;
    private string? _connectionString;
    private FunforumDbContext _db = null!;
    private AuditLogger _audit = null!;
    private int _adminId;
    private int _threadId;

    public AuditLogWriteTests()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable("NYACHAN_TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _schemaName = $"funforum_audit_{Guid.NewGuid():N}";
            var connectionString = new NpgsqlConnectionStringBuilder(_externalConnectionString)
            {
                SearchPath = $"{_schemaName},public",
            };
            _connectionString = connectionString.ConnectionString;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
        else if (_externalConnectionString is not null && _schemaName is not null)
        {
            await using var connection = new NpgsqlConnection(_externalConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schemaName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<FunforumDbContext>()
            .UseNpgsql(_connectionString!)
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new FunforumDbContext(options);
        if (_container is not null)
        {
            await _db.Database.EnsureCreatedAsync();
        }
        else
        {
            await _db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;");
            await _db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            var creator = _db.Database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();
        }

        _audit = new AuditLogger(_db);

        var now = DateTime.UtcNow;
        // Namespace Tests.Admin che entity Admin — dùng tên đầy đủ.
        var admin = new Entities.Admin
        {
            Username = "audit-super",
            Email = "audit-super@funforum.local",
            PasswordHash = "unused",
            Role = Roles.SuperAdmin,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Admins.Add(admin);

        var board = new Board
        {
            Slug = "audit",
            Name = "Audit Board",
            SortOrder = 0,
            CreatedAt = now,
        };
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();
        _adminId = admin.Id;

        var thread = new ForumThread
        {
            BoardId = board.Id,
            AuthorAdminId = admin.Id,
            Title = "Audit thread",
            Content = "before hide",
            Status = "visible",
            LastReplyAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Threads.Add(thread);
        await _db.SaveChangesAsync();
        _threadId = thread.Id;
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else if (_externalConnectionString is not null && _schemaName is not null)
        {
            await using var connection = new NpgsqlConnection(_externalConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private ClaimsPrincipal AdminPrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", _adminId.ToString()),
            new Claim(Roles.ClaimType, Roles.SuperAdmin),
        ], authenticationType: "test"));

    private AdminThreadService CreateThreadAdminService()
    {
        var threadService = new ThreadService(
            _db,
            new NoopImageProcessingService(),
            Options.Create(new ContentLimitsOptions()));
        return new AdminThreadService(
            _db,
            threadService,
            _audit,
            NullLogger<AdminThreadService>.Instance);
    }

    private async Task<List<AuditLog>> LogsForAsync(string targetType, long targetId) =>
        await _db.AuditLogs.AsNoTracking()
            .Where(l => l.TargetType == targetType && l.TargetId == targetId)
            .OrderBy(l => l.Id)
            .ToListAsync();

    [PostgreSqlFact]
    public async Task HideThread_WritesExactlyOneHideLog_WithPreviousStatus()
    {
        var service = CreateThreadAdminService();

        await service.SetStatusAsync(_threadId, "hidden", _adminId, CancellationToken.None);

        var logs = await LogsForAsync(AuditTargetTypes.Thread, _threadId);
        Assert.Single(logs);
        Assert.Equal(AuditActions.Hide, logs[0].Action);
        Assert.Equal(_adminId, logs[0].ActorAdminId);
        using var doc = JsonDocument.Parse(logs[0].Metadata!);
        Assert.Equal("visible", doc.RootElement.GetProperty("status").GetString());
    }

    [PostgreSqlFact]
    public async Task DeleteThread_WritesExactlyOneDeleteLog_WithTitleAndStatus()
    {
        var service = CreateThreadAdminService();
        await service.DeleteAsync(_threadId, AdminPrincipal(), CancellationToken.None);

        var logs = await LogsForAsync(AuditTargetTypes.Thread, _threadId);
        Assert.Single(logs);
        Assert.Equal(AuditActions.Delete, logs[0].Action);
        using var doc = JsonDocument.Parse(logs[0].Metadata!);
        Assert.Equal("Audit thread", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("visible", doc.RootElement.GetProperty("status").GetString());
    }

    [PostgreSqlFact]
    public async Task BoardDelete_WritesMetadataEnoughToRestore()
    {
        var emptyBoard = new Board
        {
            Slug = "empty-audit",
            Name = "Empty",
            Description = "restore me",
            SortOrder = 7,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Boards.Add(emptyBoard);
        await _db.SaveChangesAsync();
        var boardId = emptyBoard.Id;

        var service = new AdminBoardService(_db, _audit, NullLogger<AdminBoardService>.Instance);
        await service.DeleteAsync(boardId, _adminId, CancellationToken.None);

        var logs = await LogsForAsync(AuditTargetTypes.Board, boardId);
        Assert.Single(logs);
        Assert.Equal(AuditActions.BoardDelete, logs[0].Action);
        using var doc = JsonDocument.Parse(logs[0].Metadata!);
        Assert.Equal("empty-audit", doc.RootElement.GetProperty("slug").GetString());
        Assert.Equal("Empty", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("restore me", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("sortOrder").GetInt32());
    }

    [PostgreSqlFact]
    public async Task RoleChange_WritesPreviousRoleInMetadata()
    {
        var now = DateTime.UtcNow;
        var target = new Entities.Admin
        {
            Username = "audit-janitor",
            Email = "audit-janitor@funforum.local",
            PasswordHash = "unused",
            Role = Roles.Janitor,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Admins.Add(target);
        await _db.SaveChangesAsync();

        var service = new AdminAdminService(
            _db,
            new BCryptPasswordHasher(),
            _audit,
            NullLogger<AdminAdminService>.Instance);
        await service.ChangeRoleAsync(
            target.Id,
            new UpdateAdminRoleRequest { Role = Roles.Moderator },
            _adminId,
            CancellationToken.None);

        var logs = await LogsForAsync(AuditTargetTypes.Admin, target.Id);
        Assert.Single(logs);
        Assert.Equal(AuditActions.RoleChange, logs[0].Action);
        using var doc = JsonDocument.Parse(logs[0].Metadata!);
        Assert.Equal(Roles.Janitor, doc.RootElement.GetProperty("role").GetString());
    }

    [PostgreSqlFact]
    public async Task ListAuditLogs_FiltersByActionAndActor()
    {
        _audit.Write(_adminId, AuditActions.Hide, AuditTargetTypes.Thread, _threadId, metadata: new { status = "visible" });
        _audit.Write(_adminId, AuditActions.Unhide, AuditTargetTypes.Thread, _threadId, metadata: new { status = "hidden" });
        await _audit.FlushAsync();

        var service = new AdminAuditLogService(_db);
        var hidden = await service.ListAsync(
            1, 20, _adminId, AuditActions.Hide, null, null, null, null, CancellationToken.None);
        Assert.Equal(1, hidden.TotalItems);
        Assert.Equal(AuditActions.Hide, hidden.Items[0].Action);
        Assert.Equal(_adminId, hidden.Items[0].ActorAdminId);
        Assert.Equal("audit-super", hidden.Items[0].ActorUsername);
    }

    private sealed class NoopImageProcessingService : IImageProcessingService
    {
        public Task<ProcessedImage> ProcessAndSaveAsync(
            Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct) =>
            throw new NotSupportedException();

        public void DeleteFiles(string storedName, string thumbName)
        {
        }
    }
}
