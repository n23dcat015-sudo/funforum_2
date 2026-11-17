using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

public sealed class UpdateAdminRoleRequest
{
    [Required, MaxLength(12)]
    public string Role { get; init; } = string.Empty;
}
