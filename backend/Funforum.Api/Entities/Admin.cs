using Funforum.Api.Common;

namespace Funforum.Api.Entities;

// Bảng "admins" — current_state.md mục 3.2. Độc lập với users.Id (mục 5).
public class Admin
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    // X.1: thay bool is_super_admin bằng role 3 cấp, tránh 2 nguồn sự thật.
    public string Role { get; set; } = Roles.Moderator;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
