namespace Funforum.Api.Entities;

// Bảng "user_identities" — danh tính bên thứ ba liên kết với user local
// (auth_google_email_plan.md mục 2.1). Phase A chỉ tạo bảng + entity; luồng Google
// thuộc Phase D.
public class UserIdentity
{
    public long Id { get; set; }

    public int UserId { get; set; }

    // varchar(20), CHECK provider IN ('google') — mở rộng sau.
    public string Provider { get; set; } = string.Empty;

    // Định danh phía provider (Google "sub").
    public string ProviderUserId { get; set; } = string.Empty;

    // citext — email provider trả về lúc liên kết.
    public string ProviderEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
