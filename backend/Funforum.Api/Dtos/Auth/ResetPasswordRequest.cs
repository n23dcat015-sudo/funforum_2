using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase C — đặt lại mật khẩu bằng token dùng 1 lần (đọc từ link mail).
public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
