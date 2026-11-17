using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase C — yêu cầu gửi link đặt lại mật khẩu. Endpoint luôn trả 204 để không lộ
// email nào đã đăng ký (auth_google_email_plan.md §7).
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;
}
