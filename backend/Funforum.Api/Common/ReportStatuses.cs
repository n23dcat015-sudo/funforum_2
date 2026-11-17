namespace Funforum.Api.Common;

// X.3 — trạng thái của một report trong hàng chờ duyệt.
public static class ReportStatuses
{
    public const string Pending = "pending";
    public const string Escalated = "escalated";
    public const string Resolved = "resolved";

    public static readonly string[] All = [Pending, Escalated, Resolved];

    public static bool IsValid(string status) => All.Contains(status);
}

// X.3 — loại nội dung bị report (khớp CHECK target_type trong DB).
public static class ReportTargetTypes
{
    public const string Thread = "thread";
    public const string Reply = "reply";

    public static readonly string[] All = [Thread, Reply];

    public static bool IsValid(string targetType) => All.Contains(targetType);
}
