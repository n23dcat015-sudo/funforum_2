using System.Text.Json;

namespace Funforum.Api.Dtos.Admin;

public sealed class AuditLogDto
{
    public long Id { get; init; }
    public int? ActorAdminId { get; init; }
    public string? ActorUsername { get; init; }
    public string Action { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public long TargetId { get; init; }
    public string? Reason { get; init; }

    // Trả về JSON thật (không phải string) để frontend render trực tiếp.
    public JsonElement? Metadata { get; init; }

    public DateTime CreatedAt { get; init; }
}
