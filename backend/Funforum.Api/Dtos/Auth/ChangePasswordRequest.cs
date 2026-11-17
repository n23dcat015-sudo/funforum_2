using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
