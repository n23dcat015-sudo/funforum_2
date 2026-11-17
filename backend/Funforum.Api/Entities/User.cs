namespace Funforum.Api.Entities;

// Bảng "users" — current_state.md mục 3.2.
public class User
{
    public int Id { get; set; }

    // citext ở DB (không phân biệt hoa/thường) — cấu hình trong FunforumDbContext.
    // Phase A (auth_google_email_plan.md mục 3): username tự sinh và KHÔNG hiển thị,
    // chỉ dùng nội bộ; login của user chuyển sang email.
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // citext, mặc định "Anonymous" — tên hiển thị công khai, không unique.
    public string DisplayName { get; set; } = "Anonymous";

    // NULL khi tài khoản tạo bằng Google (chưa từng đặt mật khẩu).
    public string? PasswordHash { get; set; }

    public string? FullName { get; set; }
    public short? BirthYear { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    // X.4 — "active" | "banned" | "banned_permanently".
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
