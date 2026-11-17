using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Auth;
using Funforum.Api.Entities;
using Funforum.Api.Security;

namespace Funforum.Api.Services;

public class AuthService(
    FunforumDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService jwtTokenService,
    IBanService banService,
    IEmailSender emailSender,
    ILogger<AuthService> logger,
    IGoogleTokenValidator googleTokenValidator,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    // Phase A: username tự sinh nên đụng unique index là rất hiếm; thử lại vài lần rồi bỏ cuộc.
    private const int MaxUsernameAttempts = 5;

    private const string GoogleProvider = "google";

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<int> RegisterUserAsync(RegisterRequest request, string? ip, CancellationToken ct)
    {
        // X.4 — chặn đăng ký mới từ IP đang bị ban (thay thế nhẹ cho fingerprinting).
        if (!string.IsNullOrWhiteSpace(ip) && await banService.IsIpBannedAsync(ip, ct))
        {
            throw new ApiException(403, "Registration from this IP address is not allowed.");
        }

        // citext ở DB nên so sánh này đã không phân biệt hoa/thường.
        var emailTaken = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailTaken)
        {
            throw new ApiException(409, "Email is already registered.");
        }

        for (var attempt = 1; attempt <= MaxUsernameAttempts; attempt++)
        {
            var now = DateTime.UtcNow;
            var user = new User
            {
                Username = UsernameGenerator.Generate(),
                Email = request.Email,
                DisplayName = "Anonymous",
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
                PasswordHash = hasher.Hash(request.Password),
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync(ct);

                // Phase B — mail chào mừng gửi SAU khi commit; lỗi mail không làm fail đăng ký.
                await SendWelcomeEmailAsync(user, ct);
                return user.Id;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraintName))
            {
                db.Entry(user).State = EntityState.Detached;

                // Đụng email (race với request đăng ký khác) → trả lỗi luôn; đụng username thì sinh lại.
                if (constraintName != "ix_users_username")
                {
                    throw new ApiException(409, "Email is already registered.");
                }
            }
        }

        throw new ApiException(409, "Could not allocate a username. Please try again.");
    }

    public async Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginUserAsync(
        LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, ct);

        // Tài khoản tạo bằng Google không có PasswordHash → gợi ý dùng Google Sign-In.
        if (user is null || user.PasswordHash is null)
        {
            throw new ApiException(401, "Invalid email or password.");
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ApiException(401, "Invalid email or password.");
        }

        return await IssueSessionAsync(user, ct);
    }

    public async Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginAdminAsync(
        AdminLoginRequest request, CancellationToken ct)
    {
        var admin = await db.Admins.SingleOrDefaultAsync(a => a.Username == request.Username, ct);
        if (admin is null || !hasher.Verify(request.Password, admin.PasswordHash))
        {
            throw new ApiException(401, "Invalid username or password.");
        }

        var role = admin.Role;
        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(admin.Id, role);
        var (rawRefresh, refreshExpiresAt) = await IssueRefreshTokenAsync(null, admin.Id, ct);

        return (new AuthResponse { AccessToken = accessToken, ExpiresAt = expiresAt }, rawRefresh, refreshExpiresAt);
    }

    // Phase D — đăng nhập/đăng ký bằng Google ID token (auth_google_email_plan.md §3 Phase D).
    public async Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginWithGoogleAsync(
        GoogleLoginRequest request, CancellationToken ct)
    {
        var google = await googleTokenValidator.ValidateAsync(request.IdToken, ct);

        // 1. Danh tính Google đã liên kết → đăng nhập luôn.
        var identity = await db.UserIdentities
            .SingleOrDefaultAsync(i => i.Provider == GoogleProvider && i.ProviderUserId == google.Sub, ct);
        if (identity is not null)
        {
            var linkedUser = await db.Users.FindAsync([identity.UserId], ct)
                ?? throw new ApiException(401, "Account no longer exists.");
            return await IssueSessionAsync(linkedUser, ct);
        }

        // 2. Chưa liên kết nhưng email đã có tài khoản local.
        var existingUser = await db.Users.SingleOrDefaultAsync(u => u.Email == google.Email, ct);
        if (existingUser is not null)
        {
            // Chỉ tự động liên kết khi Google khẳng định email đã verify; nếu không, bắt đăng
            // nhập bằng mật khẩu trước để tránh chiếm tài khoản (§7).
            if (!google.EmailVerified)
            {
                throw new ApiException(409,
                    "This email is already registered. Sign in with your password before linking Google.");
            }

            db.UserIdentities.Add(new UserIdentity
            {
                UserId = existingUser.Id,
                Provider = GoogleProvider,
                ProviderUserId = google.Sub,
                ProviderEmail = google.Email,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            return await IssueSessionAsync(existingUser, ct);
        }

        // 3. Tài khoản mới hoàn toàn: username tự sinh, không mật khẩu.
        for (var attempt = 1; attempt <= MaxUsernameAttempts; attempt++)
        {
            var now = DateTime.UtcNow;
            var user = new User
            {
                Username = UsernameGenerator.Generate(),
                Email = google.Email,
                DisplayName = "Anonymous",
                PasswordHash = null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraintName))
            {
                db.ChangeTracker.Clear();

                // Username trùng ngẫu nhiên → sinh lại; đụng email (race đăng ký) → báo thử lại.
                if (constraintName == "ix_users_username")
                {
                    continue;
                }

                throw new ApiException(409, "Google sign-in could not be completed. Please try again.");
            }

            // Có user.Id rồi mới gắn danh tính Google (UserIdentity không có navigation).
            db.UserIdentities.Add(new UserIdentity
            {
                UserId = user.Id,
                Provider = GoogleProvider,
                ProviderUserId = google.Sub,
                ProviderEmail = google.Email,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);

            await SendWelcomeEmailAsync(user, ct);
            return await IssueSessionAsync(user, ct);
        }

        throw new ApiException(409, "Could not allocate a username. Please try again.");
    }

    // Phase A: một chỗ duy nhất cấp phiên cho user (JWT + refresh), dùng cho đăng nhập bằng
    // mật khẩu hiện tại và (Phase D) đăng nhập Google.
    public async Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> IssueSessionAsync(
        User user, CancellationToken ct)
    {
        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(user.Id, Roles.User);
        var (rawRefresh, refreshExpiresAt) = await IssueRefreshTokenAsync(user.Id, null, ct);

        return (new AuthResponse { AccessToken = accessToken, ExpiresAt = expiresAt }, rawRefresh, refreshExpiresAt);
    }

    public async Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> RefreshAsync(
        string rawRefreshToken, CancellationToken ct)
    {
        var hash = RefreshTokenGenerator.Hash(rawRefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt < DateTime.UtcNow)
        {
            throw new ApiException(401, "Refresh token is invalid or expired.");
        }

        // Thu hồi nguyên tử: nếu 2 request refresh cùng token chạy đồng thời, chỉ
        // request nào đổi được revoked_at từ NULL mới thắng — roadmap GĐ2 edge case.
        var rowsAffected = await db.RefreshTokens
            .Where(rt => rt.Id == token.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);

        if (rowsAffected == 0)
        {
            throw new ApiException(401, "Refresh token has already been used.");
        }

        int subjectId;
        string role;

        if (token.UserId is int userId)
        {
            subjectId = userId;
            role = Roles.User;
        }
        else
        {
            var admin = await db.Admins.FindAsync([token.AdminId!.Value], ct)
                ?? throw new ApiException(401, "Account no longer exists.");
            subjectId = admin.Id;
            role = admin.Role;
        }

        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(subjectId, role);
        var (rawNewRefresh, refreshExpiresAt) = await IssueRefreshTokenAsync(token.UserId, token.AdminId, ct);

        return (new AuthResponse { AccessToken = accessToken, ExpiresAt = expiresAt }, rawNewRefresh, refreshExpiresAt);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct)
    {
        var hash = RefreshTokenGenerator.Hash(rawRefreshToken);
        await db.RefreshTokens
            .Where(rt => rt.TokenHash == hash && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);
    }

    private async Task<(string RawToken, DateTime ExpiresAt)> IssueRefreshTokenAsync(
        int? userId, int? adminId, CancellationToken ct)
    {
        var raw = RefreshTokenGenerator.GenerateRawToken();
        var expiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = RefreshTokenGenerator.Hash(raw),
            UserId = userId,
            AdminId = adminId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return (raw, expiresAt);
    }

    // Phase B — gửi mail chào mừng, nuốt mọi lỗi để không chặn đăng ký (auth_google_email_plan.md).
    private async Task SendWelcomeEmailAsync(User user, CancellationToken ct)
    {
        try
        {
            var (subject, body) = EmailTemplates.Welcome(user.DisplayName);
            await emailSender.SendAsync(user.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send welcome email to {Email}.", user.Email);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex, out string? constraintName)
    {
        if (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            constraintName = postgres.ConstraintName;
            return true;
        }

        constraintName = null;
        return false;
    }
}
