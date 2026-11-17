using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Entities;
using Funforum.Api.Security;

namespace Funforum.Api.Services;

public interface IPasswordResetService
{
    // Luôn im lặng: không tiết lộ email có tồn tại hay không.
    Task RequestResetAsync(string email, CancellationToken ct);

    Task ResetAsync(string token, string newPassword, CancellationToken ct);
}

// Phase C — quên / đặt lại mật khẩu (auth_google_email_plan.md §3 Phase C).
public class PasswordResetService(
    FunforumDbContext db,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    // TTL link reset — khớp nội dung mail dựng ở EmailTemplates.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task RequestResetAsync(string email, CancellationToken ct)
    {
        // Không có user thì thoát êm (endpoint vẫn 204) để tránh enumeration.
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return;
        }

        var rawToken = RefreshTokenGenerator.GenerateRawToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        // Mail lỗi không được làm request fail (và cũng không lộ gì qua phản hồi).
        try
        {
            var (subject, body) = EmailTemplates.ResetPassword(frontendOptions.Value.BaseUrl, rawToken);
            await emailSender.SendAsync(user.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send password reset email to {Email}.", user.Email);
        }
    }

    public async Task ResetAsync(string token, string newPassword, CancellationToken ct)
    {
        var hash = RefreshTokenGenerator.Hash(token);
        var record = await db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (record is null || record.UsedAt is not null || record.ExpiresAt < DateTime.UtcNow)
        {
            throw new ApiException(400, "This password reset link is invalid or has expired.");
        }

        var user = await db.Users.FindAsync([record.UserId], ct)
            ?? throw new ApiException(400, "This password reset link is invalid or has expired.");

        var now = DateTime.UtcNow;
        user.PasswordHash = hasher.Hash(newPassword);
        user.UpdatedAt = now;
        record.UsedAt = now;

        // Đổi mật khẩu → thu hồi mọi refresh token cũ, giống UsersController.ChangePassword.
        await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now), ct);

        await db.SaveChangesAsync(ct);
    }
}
