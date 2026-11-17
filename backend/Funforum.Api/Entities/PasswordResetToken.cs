namespace Funforum.Api.Entities;

// Bảng "password_reset_tokens" — token đặt lại mật khẩu dùng 1 lần
// (auth_google_email_plan.md mục 2.1). Phase A chỉ tạo bảng + entity; luồng
// quên/đặt lại mật khẩu thuộc Phase C.
public class PasswordResetToken
{
    public long Id { get; set; }

    public int UserId { get; set; }

    // SHA-256 hex của token thô (giống RefreshToken.TokenHash) — token thô không lưu DB.
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    // NULL khi chưa dùng; chỉ dùng được 1 lần.
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
