using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

public class UpdateProfileRequest
{
    // Phase A: tên hiển thị công khai (không unique). Null/trắng = giữ nguyên.
    [StringLength(100)]
    public string? DisplayName { get; set; }

    [StringLength(100)]
    public string? FullName { get; set; }

    // Kiểm tra khoảng 1900..năm hiện tại trong controller (không dùng hằng số biên dịch).
    public short? BirthYear { get; set; }

    [StringLength(255)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }
}
