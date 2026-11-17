namespace Funforum.Api.Entities;

// X.2 — bảng "audit_logs": dấu vết hành động ghi (mutating) của admin, bổ sung cho
// structured logging đã có ở GĐ4. Metadata lưu trạng thái TRƯỚC khi sửa để X.7 có
// thể rollback.
public class AuditLog
{
    public long Id { get; set; }

    // NULL khi admin đã bị xoá (FK ON DELETE SET NULL): hành động vẫn phải còn dấu vết
    // ngay cả khi người thực hiện không còn.
    public int? ActorAdminId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public string? Reason { get; set; }

    // JSONB — Npgsql map string <-> jsonb.
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }
}
