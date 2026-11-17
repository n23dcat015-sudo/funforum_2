using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Auth;
using Funforum.Api.Dtos.Reports;
using Funforum.Api.Entities;
using Funforum.Api.Security;
using Funforum.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Funforum.Api.Tests.Integration;

// X.3 (hàng chờ report) + X.4 (ban tài khoản/IP) ở tầng service + DB.
public sealed class ReportAndBanTests : IAsyncLifetime
{
    private const string AuthorIp = "203.0.113.9";

    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnectionString;
    private readonly string? _schemaName;
    private string? _connectionString;
    private FunforumDbContext _db = null!;
    private AuditLogger _audit = null!;
    private int _adminId;
    private int _authorId;
    private int _otherUserId;
    private int _threadId;

    public ReportAndBanTests()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable("NYACHAN_TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _schemaName = $"funforum_repban_{Guid.NewGuid():N}";
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
        // Namespace Tests.Integration không đụng entity Admin — vẫn dùng tên đầy đủ cho rõ.
        var admin = new Entities.Admin
        {
            Username = "repban-admin",
            Email = "repban-admin@funforum.local",
            PasswordHash = "unused",
            Role = Roles.SuperAdmin,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var author = new User
        {
            Username = "ban-target",
            Email = "ban-target@funforum.local",
            PasswordHash = "unused",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var other = new User
        {
            Username = "other-user",
            Email = "other-user@funforum.local",
            PasswordHash = "unused",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var board = new Board { Slug = "repban", Name = "RepBan", SortOrder = 0, CreatedAt = now };

        _db.Admins.Add(admin);
        _db.Users.AddRange(author, other);
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        _adminId = admin.Id;
        _authorId = author.Id;
        _otherUserId = other.Id;

        var thread = new ForumThread
        {
            BoardId = board.Id,
            AuthorUserId = author.Id,
            Title = "Reportable thread",
            Content = "content",
            Status = "visible",
            PosterIp = AuthorIp,
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

    private static ClaimsPrincipal UserPrincipal(int id) =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", id.ToString()),
            new Claim(Roles.ClaimType, Roles.User),
        ], authenticationType: "test"));

    // --- X.3 ---

    [PostgreSqlFact]
    public async Task Report_Escalate_IsVisibleInEscalatedQueue_ThenResolve_LeavesContentUntouched()
    {
        var reportService = new ReportService(_db);
        var reportId = await reportService.CreateAsync(
            new CreateReportRequest
            {
                TargetType = ReportTargetTypes.Thread,
                TargetId = _threadId,
                Reason = "spam",
            },
            UserPrincipal(_otherUserId), CancellationToken.None);

        var adminReports = new AdminReportService(_db);

        var escalated = await adminReports.EscalateAsync(reportId, CancellationToken.None);
        Assert.Equal(ReportStatuses.Escalated, escalated.Status);

        // Moderator lọc ?status=escalated thấy report trong queue.
        var queue = await adminReports.ListAsync(
            1, 20, ReportStatuses.Escalated, null, null, null, CancellationToken.None);
        Assert.Equal(1, queue.TotalItems);
        Assert.Equal(reportId, queue.Items[0].Id);
        Assert.Equal("other-user", queue.Items[0].ReporterUsername);

        var resolved = await adminReports.ResolveAsync(reportId, CancellationToken.None);
        Assert.Equal(ReportStatuses.Resolved, resolved.Status);

        var afterResolve = await adminReports.ListAsync(
            1, 20, ReportStatuses.Escalated, null, null, null, CancellationToken.None);
        Assert.Empty(afterResolve.Items);

        // KHÔNG tạo cờ is_deleted/ẩn riêng: nội dung vẫn visible.
        var thread = await _db.Threads.AsNoTracking().SingleAsync(t => t.Id == _threadId);
        Assert.Equal("visible", thread.Status);
    }

    [PostgreSqlFact]
    public async Task Report_UnknownTarget_Throws404()
    {
        var reportService = new ReportService(_db);
        var ex = await Assert.ThrowsAsync<ApiException>(() => reportService.CreateAsync(
            new CreateReportRequest { TargetType = ReportTargetTypes.Reply, TargetId = 999999, Reason = "x" },
            UserPrincipal(_otherUserId), CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }

    // --- X.4 ---

    [PostgreSqlFact]
    public async Task Ban_FromThreadIp_BlocksUserAndIp_UnbanRestores_WritesAuditLogs()
    {
        var bans = new BanService(_db, _audit);

        var dto = await bans.CreateAsync(
            new CreateBanRequest { UserId = _authorId, Reason = "abuse", ThreadId = _threadId },
            _adminId, CancellationToken.None);

        Assert.Equal(BanStatuses.Active, dto.Status);
        Assert.Equal(AuthorIp, dto.BannedIp); // IP lấy từ bài bị report.

        Assert.Equal(
            UserStatuses.Banned,
            (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _authorId)).Status);

        // Chặn theo user_id và theo IP (bắt tài khoản phụ cùng IP).
        Assert.True(await bans.IsBlockedAsync(_authorId, null, CancellationToken.None));
        Assert.True(await bans.IsBlockedAsync(_otherUserId, AuthorIp, CancellationToken.None));
        Assert.False(await bans.IsBlockedAsync(_otherUserId, "198.51.100.1", CancellationToken.None));

        var banLogs = await _db.AuditLogs.AsNoTracking()
            .Where(l => l.Action == AuditActions.Ban && l.TargetType == AuditTargetTypes.User && l.TargetId == _authorId)
            .ToListAsync();
        Assert.Single(banLogs);
        using (var doc = JsonDocument.Parse(banLogs[0].Metadata!))
        {
            Assert.Equal(UserStatuses.Active, doc.RootElement.GetProperty("status").GetString());
        }

        await bans.UnbanAsync(dto.Id, _adminId, CancellationToken.None);

        Assert.Equal(
            UserStatuses.Active,
            (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _authorId)).Status);
        Assert.False(await bans.IsBlockedAsync(_authorId, null, CancellationToken.None));
        Assert.False(await bans.IsBlockedAsync(_otherUserId, AuthorIp, CancellationToken.None));

        var unbanLogs = await _db.AuditLogs.AsNoTracking()
            .Where(l => l.Action == AuditActions.Unban && l.TargetId == _authorId)
            .ToListAsync();
        Assert.Single(unbanLogs);
    }

    [PostgreSqlFact]
    public async Task Register_FromBannedIp_IsBlocked()
    {
        var bans = new BanService(_db, _audit);
        await bans.CreateAsync(
            new CreateBanRequest { UserId = _authorId, Reason = "abuse", BannedIp = AuthorIp },
            _adminId, CancellationToken.None);

        var auth = new AuthService(
            _db, new BCryptPasswordHasher(), new StubJwtTokenService(), bans, null!, null!, null!, Options.Create(new JwtOptions()));

        var ex = await Assert.ThrowsAsync<ApiException>(() => auth.RegisterUserAsync(
            new RegisterRequest { Email = "newbie@funforum.local", Password = "password123" },
            AuthorIp, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    private sealed class StubJwtTokenService : IJwtTokenService
    {
        public (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(int subjectId, string role) =>
            throw new NotSupportedException();
    }
}
