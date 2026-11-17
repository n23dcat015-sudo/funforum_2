namespace Funforum.Api.Dtos.Auth;

public class MeResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public short? BirthYear { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    // X.4/X.5 — "active" | "banned" | "banned_permanently", để FE biết có phải chuyển tới /appeal.
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
}
