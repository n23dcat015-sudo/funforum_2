using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Auth;
using Funforum.Api.Entities;
using Funforum.Api.Security;
using Funforum.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Funforum.Api.Tests.Integration;

// Phase F — luồng auth mới: đăng ký/đăng nhập bằng email, Google Sign-In (liên kết + tạo
// mới), quên/đặt lại mật khẩu. Cần PostgreSQL (Docker hoặc NYACHAN_TEST_POSTGRES_CONNECTION).
public sealed class AuthEmailGoogleTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnectionString;
    private readonly string? _schemaName;
    private string? _connectionString;
    private FunforumDbContext _db = null!;
    private AuditLogger _audit = null!;

    public AuthEmailGoogleTests()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable("NYACHAN_TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _schemaName = $"funforum_auth_{Guid.NewGuid():N}";
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

    private AuthService CreateAuthService(FakeEmailSender email, StubGoogleTokenValidator google) =>
        new(_db, new BCryptPasswordHasher(), new StubJwtTokenService(), new BanService(_db, _audit),
            email, NullLogger<AuthService>.Instance, google, Options.Create(new JwtOptions()));

    private PasswordResetService CreatePasswordResetService(FakeEmailSender email) =>
        new(_db, new BCryptPasswordHasher(), email, Options.Create(new FrontendOptions()),
            NullLogger<PasswordResetService>.Instance);

    private async Task<User> SeedUserAsync(string email, string password)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = UsernameGenerator.Generate(),
            Email = email,
            DisplayName = "Anonymous",
            PasswordHash = new BCryptPasswordHasher().Hash(password),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task SeedResetTokenAsync(int userId, string rawToken, DateTime expiresAt)
    {
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private void SeedRefreshToken(int? userId, int? adminId, string rawToken, DateTime expiresAt) =>
        _db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            UserId = userId,
            AdminId = adminId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });

    // --- Đăng ký ---

    [PostgreSqlFact]
    public async Task Register_AutoGeneratesUsername_AnonymousDisplayName_SendsWelcomeEmail()
    {
        var email = new FakeEmailSender();
        var auth = CreateAuthService(email, new StubGoogleTokenValidator());

        var id = await auth.RegisterUserAsync(
            new RegisterRequest { Email = "alice@funforum.local", Password = "password123" },
            ip: null, CancellationToken.None);

        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.StartsWith("anon_", user.Username);
        Assert.Equal("Anonymous", user.DisplayName);
        Assert.Null(user.FullName);
        Assert.NotNull(user.PasswordHash);

        var mail = Assert.Single(email.Sent);
        Assert.Equal("alice@funforum.local", mail.To);
        Assert.Contains("Welcome", mail.Subject);
    }

    [PostgreSqlFact]
    public async Task Register_TwoUsers_GetDistinctUsernamesButSameAnonymousDisplayName()
    {
        var email = new FakeEmailSender();
        var auth = CreateAuthService(email, new StubGoogleTokenValidator());

        var firstId = await auth.RegisterUserAsync(
            new RegisterRequest { Email = "u1@funforum.local", Password = "password123" }, null, CancellationToken.None);
        var secondId = await auth.RegisterUserAsync(
            new RegisterRequest { Email = "u2@funforum.local", Password = "password123" }, null, CancellationToken.None);

        var users = await _db.Users.AsNoTracking().Where(u => u.Id == firstId || u.Id == secondId).ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.NotEqual(users[0].Username, users[1].Username);
        Assert.All(users, u => Assert.Equal("Anonymous", u.DisplayName));
    }

    // --- Đăng nhập bằng email ---

    [PostgreSqlFact]
    public async Task Login_ByEmail_Succeeds_AndWrongPasswordReturns401()
    {
        var email = new FakeEmailSender();
        var auth = CreateAuthService(email, new StubGoogleTokenValidator());
        await SeedUserAsync("bob@funforum.local", "password123");

        var (auth1, rawRefresh, _) = await auth.LoginUserAsync(
            new LoginRequest { Email = "bob@funforum.local", Password = "password123" }, CancellationToken.None);
        Assert.NotEmpty(auth1.AccessToken);
        Assert.NotEmpty(rawRefresh);

        var wrong = await Assert.ThrowsAsync<ApiException>(() => auth.LoginUserAsync(
            new LoginRequest { Email = "bob@funforum.local", Password = "wrong-password" }, CancellationToken.None));
        Assert.Equal(401, wrong.StatusCode);

        var unknown = await Assert.ThrowsAsync<ApiException>(() => auth.LoginUserAsync(
            new LoginRequest { Email = "ghost@funforum.local", Password = "password123" }, CancellationToken.None));
        Assert.Equal(401, unknown.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Login_GoogleOnlyAccount_WithPassword_Returns401()
    {
        var email = new FakeEmailSender();
        var google = new StubGoogleTokenValidator
        {
            Info = new GoogleUserInfo("google-sub-carol", "carol@funforum.local", EmailVerified: true, Name: "Carol"),
        };
        var auth = CreateAuthService(email, google);

        // Tài khoản tạo bằng Google không có PasswordHash.
        await auth.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ApiException>(() => auth.LoginUserAsync(
            new LoginRequest { Email = "carol@funforum.local", Password = "whatever123" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    // --- Google Sign-In ---

    [PostgreSqlFact]
    public async Task Google_FirstLogin_CreatesUserWithIdentity_SecondLoginReusesAccount()
    {
        var email = new FakeEmailSender();
        var google = new StubGoogleTokenValidator
        {
            Info = new GoogleUserInfo("google-sub-dave", "dave@funforum.local", EmailVerified: true, Name: "Dave"),
        };
        var auth = CreateAuthService(email, google);

        await auth.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None);
        await auth.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None);

        var user = Assert.Single(await _db.Users.AsNoTracking().Where(u => u.Email == "dave@funforum.local").ToListAsync());
        Assert.Null(user.PasswordHash);
        Assert.Equal("Anonymous", user.DisplayName);

        var identity = Assert.Single(await _db.UserIdentities.AsNoTracking().ToListAsync());
        Assert.Equal("google", identity.Provider);
        Assert.Equal("google-sub-dave", identity.ProviderUserId);
        Assert.Equal(user.Id, identity.UserId);

        // Mail chào mừng chỉ gửi ở lần tạo tài khoản đầu tiên.
        Assert.Single(email.Sent);
    }

    [PostgreSqlFact]
    public async Task Google_ExistingLocalEmail_Verified_LinksIdentityWithoutNewUser()
    {
        var email = new FakeEmailSender();
        var google = new StubGoogleTokenValidator
        {
            Info = new GoogleUserInfo("google-sub-erin", "erin@funforum.local", EmailVerified: true, Name: "Erin"),
        };
        var auth = CreateAuthService(email, google);

        var localUser = await SeedUserAsync("erin@funforum.local", "password123");
        var before = await _db.Users.AsNoTracking().CountAsync();

        await auth.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None);

        Assert.Equal(before, await _db.Users.AsNoTracking().CountAsync());
        var identity = Assert.Single(await _db.UserIdentities.AsNoTracking().ToListAsync());
        Assert.Equal(localUser.Id, identity.UserId);
        Assert.Equal("google-sub-erin", identity.ProviderUserId);
    }

    [PostgreSqlFact]
    public async Task Google_ExistingLocalEmail_NotVerified_Returns409WithoutLinking()
    {
        var email = new FakeEmailSender();
        var google = new StubGoogleTokenValidator
        {
            Info = new GoogleUserInfo("google-sub-frank", "frank@funforum.local", EmailVerified: false, Name: null),
        };
        var auth = CreateAuthService(email, google);

        await SeedUserAsync("frank@funforum.local", "password123");

        var ex = await Assert.ThrowsAsync<ApiException>(() => auth.LoginWithGoogleAsync(
            new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None));
        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(await _db.UserIdentities.AsNoTracking().ToListAsync());
    }

    // --- Quên / đặt lại mật khẩu ---

    [PostgreSqlFact]
    public async Task ForgotPassword_UnknownEmail_IsSilent_ButKnownEmail_StoresTokenAndSendsMail()
    {
        var email = new FakeEmailSender();
        var reset = CreatePasswordResetService(email);

        await reset.RequestResetAsync("nobody@funforum.local", CancellationToken.None);
        Assert.Empty(email.Sent);
        Assert.Empty(await _db.PasswordResetTokens.AsNoTracking().ToListAsync());

        await SeedUserAsync("grace@funforum.local", "password123");

        await reset.RequestResetAsync("grace@funforum.local", CancellationToken.None);

        Assert.Single(email.Sent);
        Assert.Contains("/reset-password?token=", email.Sent[0].Body);
        var token = Assert.Single(await _db.PasswordResetTokens.AsNoTracking().ToListAsync());
        Assert.Null(token.UsedAt);
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
    }

    [PostgreSqlFact]
    public async Task ResetPassword_SingleUse_SetsNewPasswordAndRevokesRefreshTokens()
    {
        var email = new FakeEmailSender();
        var reset = CreatePasswordResetService(email);

        var user = await SeedUserAsync("heidi@funforum.local", "oldpassword1");
        const string RawToken = "raw-reset-token-heidi";
        await SeedResetTokenAsync(user.Id, RawToken, DateTime.UtcNow.AddMinutes(30));
        SeedRefreshToken(user.Id, null, "existing-refresh-token", DateTime.UtcNow.AddDays(7));
        await _db.SaveChangesAsync();

        await reset.ResetAsync(RawToken, "newpassword1", CancellationToken.None);

        var updated = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(new BCryptPasswordHasher().Verify("newpassword1", updated.PasswordHash!));

        var used = await _db.PasswordResetTokens.AsNoTracking().SingleAsync();
        Assert.NotNull(used.UsedAt);

        var refresh = await _db.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.NotNull(refresh.RevokedAt);

        // Dùng lại token lần hai → 400.
        var ex = await Assert.ThrowsAsync<ApiException>(() => reset.ResetAsync(
            RawToken, "another123456", CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [PostgreSqlFact]
    public async Task ResetPassword_ExpiredToken_Returns400()
    {
        var email = new FakeEmailSender();
        var reset = CreatePasswordResetService(email);

        var user = await SeedUserAsync("ivan@funforum.local", "oldpassword1");
        const string RawToken = "raw-expired-token";
        await SeedResetTokenAsync(user.Id, RawToken, DateTime.UtcNow.AddMinutes(-1));

        var ex = await Assert.ThrowsAsync<ApiException>(() => reset.ResetAsync(
            RawToken, "newpassword1", CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);

        var untouched = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(new BCryptPasswordHasher().Verify("oldpassword1", untouched.PasswordHash!));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class StubGoogleTokenValidator : IGoogleTokenValidator
    {
        public GoogleUserInfo Info { get; set; } = new("google-sub-default", "default@funforum.local", true, null);

        public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct) => Task.FromResult(Info);
    }

    private sealed class StubJwtTokenService : IJwtTokenService
    {
        public (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(int subjectId, string role) =>
            ($"access-token-{role}-{subjectId}", DateTime.UtcNow.AddMinutes(15));
    }
}
