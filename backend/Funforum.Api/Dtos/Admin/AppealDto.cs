namespace Funforum.Api.Dtos.Admin;

// X.5 — đơn kháng cáo trong hàng chờ duyệt của Moderator+.
public sealed class AppealDto
{
    public long Id { get; init; }
    public long BanId { get; init; }
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? BannedIp { get; init; }
    public string ReasonText { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? ReviewedByAdminId { get; init; }
    public string? ReviewedByUsername { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
}

// X.5 — Moderator+ duyệt đơn: approve (mở ban) hoặc reject (giữ nguyên, khóa nộp lại).
public sealed class ReviewAppealRequest
{
    public string Decision { get; set; } = string.Empty;
}
