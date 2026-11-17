namespace Funforum.Api.Entities;

// Bảng "refresh_tokens" — current_state.md mục 3.2.
// Đúng 1 trong 2 cột UserId/AdminId có giá trị (CHECK constraint trong DbContext).
public class RefreshToken
{
    public long Id { get; set; }

    // SHA-256 hash của token thô — không bao giờ lưu token thô (mục 5).
    public string TokenHash { get; set; } = string.Empty;

    public int? UserId { get; set; }
    public int? AdminId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
