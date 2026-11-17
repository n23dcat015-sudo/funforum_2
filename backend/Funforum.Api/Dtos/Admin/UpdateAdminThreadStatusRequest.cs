using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

public sealed class UpdateAdminThreadStatusRequest
{
    [Required]
    public string Status { get; init; } = string.Empty;
}
