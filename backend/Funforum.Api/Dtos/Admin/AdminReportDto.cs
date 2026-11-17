namespace Funforum.Api.Dtos.Admin;

public sealed class AdminReportDto
{
    public long Id { get; init; }
    public string TargetType { get; init; } = string.Empty;
    public long TargetId { get; init; }
    public int? ReporterUserId { get; init; }
    public string? ReporterUsername { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
