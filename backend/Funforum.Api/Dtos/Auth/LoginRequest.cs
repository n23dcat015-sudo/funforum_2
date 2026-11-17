using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase A: user đăng nhập bằng email (auth_google_email_plan.md mục 4).
// Admin vẫn đăng nhập bằng username — xem AdminLoginRequest.
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
