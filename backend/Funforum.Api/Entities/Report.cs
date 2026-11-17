namespace Funforum.Api.Entities;

// X.3 — report do người dùng gửi cho một thread/reply.
public class Report
{
    public long Id { get; set; }

    public string TargetType { get; set; } = string.Empty; // report_target_types: thread | reply

    public long TargetId { get; set; }

    // Null khi report do hệ thống tạo (ví dụ wordfilter ở X.6).
    public int? ReporterUserId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "pending"; // pending | escalated | resolved

    public DateTime CreatedAt { get; set; }
}
