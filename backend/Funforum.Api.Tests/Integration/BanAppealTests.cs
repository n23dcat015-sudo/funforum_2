using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Appeals;
using Funforum.Api.Entities;
using Funforum.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Funforum.Api.Tests.Integration;

// X.5 (quy trình kháng cáo ban) ở tầng service + DB: ban → appeal → reject/approve.
public sealed class BanAppealTests : IAsyncLifetime
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
    private int _threadId;

    public BanAppealTests()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable("NYACHAN_TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _schemaName = $"funforum_appeal_{Guid.NewGuid():N}";
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
        var admin = new Entities.Admin
        {
            Username = "appeal-admin",
            Email = "appeal-admin@funforum.local",
            PasswordHash = "unused",
            Role = Roles.SuperAdmin,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var author = new User
        {
            Username = "appeal-target",
            Email = "appeal-target@funforum.local",
            PasswordHash = "unused",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var board = new Board { Slug = "appeal", Name = "Appeal", SortOrder = 0, CreatedAt = now };

        _db.Admins.Add(admin);
        _db.Users.Add(author);
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        _adminId = admin.Id;
        _authorId = author.Id;

        var thread = new ForumThread
        {
            BoardId = board.Id,
            AuthorUserId = author.Id,
            Title = "Bannable thread",
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

    private async Task<BanDto> BanAuthorAsync()
    {
        var bans = new BanService(_db, _audit);
        return await bans.CreateAsync(
            new CreateBanRequest { UserId = _authorId, Reason = "abuse", ThreadId = _threadId },
            _adminId, CancellationToken.None);
    }

    private Task<long> AppealIdAsync(long banId) =>
        _db.BanAppeals.AsNoTracking().Where(a => a.BanId == banId).Select(a => a.Id).SingleAsync();

    [PostgreSqlFact]
    public async Task Appeal_Rejected_LocksResubmissionForSameBan()
    {
        var ban = await BanAuthorAsync();
        var appeals = new AppealService(_db);

        var beforeSubmit = await appeals.GetMineAsync(_authorId, CancellationToken.None);
        Assert.False(beforeSubmit.AppealsLocked);
        Assert.Equal(ban.Id, beforeSubmit.BanId);
        Assert.True(beforeSubmit.CanSubmit);

        var created = await appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "It was a mistake." },
            _authorId, CancellationToken.None);
        Assert.Equal(AppealStatuses.Pending, created.AppealStatus);
        Assert.False(created.CanSubmit);

        // Đang có đơn pending → không nộp chồng.
        var duplicate = await Assert.ThrowsAsync<ApiException>(() => appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "Again." },
            _authorId, CancellationToken.None));
        Assert.Equal(409, duplicate.StatusCode);

        var adminAppeals = new AdminAppealService(_db, _audit);
        var reviewed = await adminAppeals.ReviewAsync(
            await AppealIdAsync(ban.Id), AppealDecisions.Reject, _adminId, CancellationToken.None);
        Assert.Equal(AppealStatuses.Rejected, reviewed.Status);
        Assert.Equal(_adminId, reviewed.ReviewedByAdminId);

        // Sau khi bị từ chối vẫn không nộp lại được cho cùng lệnh ban.
        var afterReject = await Assert.ThrowsAsync<ApiException>(() => appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "Third time." },
            _authorId, CancellationToken.None));
        Assert.Equal(409, afterReject.StatusCode);

        var mineAfter = await appeals.GetMineAsync(_authorId, CancellationToken.None);
        Assert.False(mineAfter.CanSubmit);
        Assert.Equal(AppealStatuses.Rejected, mineAfter.AppealStatus);
    }

    [PostgreSqlFact]
    public async Task Appeal_Approved_LiftsBanAndAllowsPostingAgain()
    {
        var ban = await BanAuthorAsync();
        Assert.Equal(
            UserStatuses.Banned,
            (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _authorId)).Status);

        var bans = new BanService(_db, _audit);
        Assert.True(await bans.IsBlockedAsync(_authorId, null, CancellationToken.None));

        var appeals = new AppealService(_db);
        await appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "Please reconsider." },
            _authorId, CancellationToken.None);

        var adminAppeals = new AdminAppealService(_db, _audit);
        var approved = await adminAppeals.ReviewAsync(
            await AppealIdAsync(ban.Id), AppealDecisions.Approve, _adminId, CancellationToken.None);
        Assert.Equal(AppealStatuses.Approved, approved.Status);

        // Approve mở ban và đưa tài khoản về active.
        Assert.Equal(
            BanStatuses.Lifted,
            (await _db.Bans.AsNoTracking().SingleAsync(b => b.Id == ban.Id)).Status);
        Assert.Equal(
            UserStatuses.Active,
            (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _authorId)).Status);
        Assert.False(await bans.IsBlockedAsync(_authorId, null, CancellationToken.None));

        var unbanLogs = await _db.AuditLogs.AsNoTracking()
            .Where(l => l.Action == AuditActions.Unban && l.TargetType == AuditTargetTypes.User && l.TargetId == _authorId)
            .ToListAsync();
        Assert.Single(unbanLogs);

        // Lệnh ban đã lifted → không nộp đơn mới cho nó được.
        var afterApprove = await Assert.ThrowsAsync<ApiException>(() => appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "One more." },
            _authorId, CancellationToken.None));
        Assert.Equal(409, afterApprove.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Appeal_LockedPermanently_BlocksSubmission()
    {
        var ban = await BanAuthorAsync();
        var adminUsers = new AdminUserService(_db, _audit, NullLogger<AdminUserService>.Instance);

        await adminUsers.LockAppealAsync(_authorId, _adminId, CancellationToken.None);
        Assert.Equal(
            UserStatuses.BannedPermanently,
            (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _authorId)).Status);

        var appeals = new AppealService(_db);
        var mine = await appeals.GetMineAsync(_authorId, CancellationToken.None);
        Assert.True(mine.AppealsLocked);
        Assert.False(mine.CanSubmit);

        var ex = await Assert.ThrowsAsync<ApiException>(() => appeals.CreateAsync(
            new CreateAppealRequest { BanId = ban.Id, ReasonText = "Let me explain." },
            _authorId, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);

        // Khóa lần nữa là xung đột.
        var duplicate = await Assert.ThrowsAsync<ApiException>(
            () => adminUsers.LockAppealAsync(_authorId, _adminId, CancellationToken.None));
        Assert.Equal(409, duplicate.StatusCode);
    }
}
