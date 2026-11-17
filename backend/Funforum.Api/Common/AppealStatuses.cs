namespace Funforum.Api.Common;

// X.5 — trạng thái của một đơn kháng cáo (khớp CHECK ban_appeals.status trong DB).
public static class AppealStatuses
{
    public const string Pending = "pending";
    public const string Rejected = "rejected";
    public const string Approved = "approved";

    public static readonly string[] All = [Pending, Rejected, Approved];

    public static bool IsValid(string status) => All.Contains(status);
}

// X.5 — quyết định khi Moderator+ duyệt đơn kháng cáo.
public static class AppealDecisions
{
    public const string Approve = "approve";
    public const string Reject = "reject";

    public static readonly string[] All = [Approve, Reject];

    public static bool IsValid(string decision) => All.Contains(decision);
}
