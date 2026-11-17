using System.ComponentModel.DataAnnotations;
using Funforum.Api.Common;

namespace Funforum.Api.Dtos.Admin;

public sealed class CreateAdminAccountRequest
{
    [Required, MinLength(3), MaxLength(32)]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(64)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(12)]
    public string Role { get; init; } = Roles.Moderator;
}
