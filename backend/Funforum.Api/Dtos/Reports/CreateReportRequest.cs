using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Reports;

public sealed class CreateReportRequest
{
    [Required]
    public string TargetType { get; set; } = string.Empty; // thread | reply

    [Required]
    public long TargetId { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}
