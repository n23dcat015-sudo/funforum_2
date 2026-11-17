namespace Funforum.Api.Dtos.Appeals;

// X.5 — user gửi đơn kháng cáo cho lệnh ban đang áp dụng lên mình.
public sealed class CreateAppealRequest
{
    public long BanId { get; set; }

    public string ReasonText { get; set; } = string.Empty;
}
