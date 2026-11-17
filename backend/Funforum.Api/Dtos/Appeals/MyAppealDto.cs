namespace Funforum.Api.Dtos.Appeals;

// X.5 — trạng thái kháng cáo hiện tại của người gọi (GET /api/appeals/me).
// BanId null nghĩa là không có lệnh ban active nào; CanSubmit = còn được nộp đơn.
public sealed class MyAppealDto
{
    public bool AppealsLocked { get; init; }

    public long? BanId { get; init; }
    public string? BanReason { get; init; }
    public string? BannedIp { get; init; }

    public string? AppealStatus { get; init; }
    public string? AppealReasonText { get; init; }
    public DateTime? AppealCreatedAt { get; init; }
    public DateTime? AppealReviewedAt { get; init; }

    public bool CanSubmit { get; init; }
}
