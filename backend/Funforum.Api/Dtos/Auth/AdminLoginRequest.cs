using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase A: admin giữ nguyên đăng nhập bằng username (/api/auth/admin/login).
public class AdminLoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
