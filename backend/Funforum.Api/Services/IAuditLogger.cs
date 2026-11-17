namespace Funforum.Api.Services;

// X.2 — ghi dấu vết hành động admin xuống bảng audit_logs.
public interface IAuditLogger
{
    // Chỉ stage vào change tracker của FunforumDbContext scoped, KHÔNG tự commit —
    // xem AuditLogger để biết lý do.
    void Write(
        int? actorAdminId,
        string action,
        string targetType,
        long targetId,
        object? metadata = null,
        string? reason = null);

    // Commit dòng log đã stage. Chỉ dùng khi service đã commit nghiệp vụ bên trong.
    Task FlushAsync(CancellationToken ct = default);
}
