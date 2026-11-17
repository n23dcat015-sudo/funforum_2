namespace Funforum.Api.Entities;

// X.5 — đơn kháng cáo do user bị ban gửi cho một bản ghi ban (bans.id).
public class BanAppeal
{
    public long Id { get; set; }

    public long BanId { get; set; }

    public int UserId { get; set; }

    public string ReasonText { get; set; } = string.Empty;

    public string Status { get; set; } = "pending"; // pending | rejected | approved

    // NULL khi chưa duyệt; SET NULL nếu admin duyệt bị xoá.
    public int? ReviewedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
