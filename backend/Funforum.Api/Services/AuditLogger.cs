using System.Text.Json;
using Funforum.Api.Data;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// Ghi vào cùng FunforumDbContext scoped với nghiệp vụ đang chạy và không tự SaveChanges:
// caller gọi Write() ngay trước SaveChangesAsync() sẵn có để dòng log commit NGUYÊN TỬ
// cùng hành động (hành động fail thì không có log mồ côi, và ngược lại). Service nào
// commit bên trong (AdminThreadService.Create/Update/Delete) thì Write() rồi FlushAsync().
public sealed class AuditLogger(FunforumDbContext db) : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Write(
        int? actorAdminId,
        string action,
        string targetType,
        long targetId,
        object? metadata = null,
        string? reason = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorAdminId = actorAdminId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions),
            CreatedAt = DateTime.UtcNow,
        });
    }

    public Task FlushAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
