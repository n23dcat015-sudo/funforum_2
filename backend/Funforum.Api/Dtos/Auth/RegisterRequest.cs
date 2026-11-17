using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase A: user không còn tự chọn username — backend tự sinh handle ẩn và đăng nhập
// bằng email (auth_google_email_plan.md mục 3, 4).
public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(100)]
    public string? FullName { get; set; }
}
