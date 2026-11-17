using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Entities;
using Funforum.Api.Security;
using Funforum.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Funforum.Api.Tests.Integration;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("NYACHAN_RUN_POSTGRES_INTEGRATION_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set NYACHAN_RUN_POSTGRES_INTEGRATION_TESTS=1 and configure PostgreSQL or Docker to run these tests.";
        }
    }
}

// current_state.md mục 3.3 + roadmap.md GĐ3e: test bắt buộc cho thuật toán đệ quy
// hidden_ancestor_count. Cần Docker hoặc NYACHAN_TEST_POSTGRES_CONNECTION trỏ tới DB
// mà tài khoản được phép CREATE/DROP SCHEMA. Với connection ngoài, mỗi test tạo schema
// ngẫu nhiên, tạo model tables trực tiếp trong schema đó, rồi chỉ xóa schema đó.
public class ReplyHiddenAncestorRecursionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnectionString;
    private readonly string? _schemaName;
    private string? _connectionString;

    public ReplyHiddenAncestorRecursionTests()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable("NYACHAN_TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _schemaName = $"funforum_test_{Guid.NewGuid():N}";
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

    private FunforumDbContext _db = null!;
    private int _threadId;

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

        var board = new Board { Slug = "test", Name = "Test", SortOrder = 0, CreatedAt = DateTime.UtcNow };
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        var thread = new ForumThread
        {
            BoardId = board.Id,
            Title = "T",
            Content = "C",
            Status = "visible",
            LastReplyAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
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

    private async Task<Reply> AddReplyAsync(int? parentId, int? rootId)
    {
        var reply = new Reply
        {
            ThreadId = _threadId,
            ParentReplyId = parentId,
            RootReplyId = rootId,
            Content = "reply",
            Status = "visible",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Replies.Add(reply);
        await _db.SaveChangesAsync();
        return reply;
    }

    [PostgreSqlFact]
    public async Task ExpiredRefreshToken_IsRejected()
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = "expired-token-test",
            Email = "expired-token-test@funforum.local",
            PasswordHash = "unused",
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        const string rawToken = "expired-stage8-refresh-token";
        _db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            UserId = user.Id,
            ExpiresAt = now.AddMinutes(-1),
            CreatedAt = now.AddDays(-1),
        });
        await _db.SaveChangesAsync();

        var service = new AuthService(_db, null!, null!, null!, null!, null!, null!, Options.Create(new JwtOptions()));
        var exception = await Assert.ThrowsAsync<ApiException>(
            () => service.RefreshAsync(rawToken, CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
    }

    // Sao chép nguyên văn từ ReplyService.UpdateDescendantHiddenCountAsync (method đó là
    // private nên không gọi thẳng được) — sửa 1 chỗ thì nhớ sửa chỗ kia.
    private Task UpdateDescendantHiddenCountAsync(int replyId, int delta) =>
        _db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH RECURSIVE descendants AS (
                SELECT id FROM replies WHERE parent_reply_id = {replyId}
                UNION ALL
                SELECT r.id FROM replies r
                INNER JOIN descendants d ON r.parent_reply_id = d.id
            )
            UPDATE replies
            SET hidden_ancestor_count = hidden_ancestor_count + {delta}
            WHERE id IN (SELECT id FROM descendants)
            """);

    [PostgreSqlFact]
    public async Task HidingReplyInMiddle_HidesDescendants_ButNotItself()
    {
        // A (gốc) -> B -> C
        var a = await AddReplyAsync(null, null);
        var b = await AddReplyAsync(a.Id, a.Id);
        var c = await AddReplyAsync(b.Id, a.Id);

        b.Status = "hidden";
        b.HiddenBy = "owner";
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(b.Id, +1);

        await _db.Entry(a).ReloadAsync();
        await _db.Entry(b).ReloadAsync();
        await _db.Entry(c).ReloadAsync();

        Assert.Equal("visible", a.Status);
        Assert.Equal(0, a.HiddenAncestorCount);
        Assert.Equal("hidden", b.Status);
        Assert.Equal(0, b.HiddenAncestorCount);
        Assert.Equal(1, c.HiddenAncestorCount);
        Assert.Equal("visible", c.Status);
    }

    [PostgreSqlFact]
    public async Task UnhidingReply_RevealsDescendants_WhenNoOtherAncestorHidden()
    {
        var a = await AddReplyAsync(null, null);
        var b = await AddReplyAsync(a.Id, a.Id);
        var c = await AddReplyAsync(b.Id, a.Id);

        b.Status = "hidden";
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(b.Id, +1);

        b.Status = "visible";
        b.HiddenBy = null;
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(b.Id, -1);

        await _db.Entry(c).ReloadAsync();
        Assert.Equal(0, c.HiddenAncestorCount);
    }

    [PostgreSqlFact]
    public async Task NestedHiding_UnhidingOuterLayer_KeepsInnerLayerHidden()
    {
        // hide A → hide B (con của A) → unhide A. Con của B (là C) phải vẫn ẩn.
        var a = await AddReplyAsync(null, null);
        var b = await AddReplyAsync(a.Id, a.Id);
        var c = await AddReplyAsync(b.Id, a.Id);

        a.Status = "hidden";
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(a.Id, +1);

        b.Status = "hidden";
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(b.Id, +1);

        a.Status = "visible";
        await _db.SaveChangesAsync();
        await UpdateDescendantHiddenCountAsync(a.Id, -1);

        await _db.Entry(b).ReloadAsync();
        await _db.Entry(c).ReloadAsync();

        Assert.Equal(0, b.HiddenAncestorCount);
        Assert.Equal("hidden", b.Status); // B vẫn cần tự unhide riêng
        Assert.Equal(1, c.HiddenAncestorCount); // vẫn ẩn lây vì B còn hidden
    }
}
