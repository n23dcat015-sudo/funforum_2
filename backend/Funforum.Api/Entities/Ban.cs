namespace Funforum.Api.Entities;

// X.4 — bản ghi ban tài khoản và/hoặc IP do Moderator+ tạo.
public class Ban
{
    public long Id { get; set; }

    public int UserId { get; set; }

    // IP ghi lúc đăng bài (threads/replies.poster_ip). Null nếu chỉ ban tài khoản.
    public string? BannedIp { get; set; }

    public string? Reason { get; set; }

    public int? BannedByAdminId { get; set; }

    public string Status { get; set; } = "active"; // active | lifted

    public DateTime CreatedAt { get; set; }
}
