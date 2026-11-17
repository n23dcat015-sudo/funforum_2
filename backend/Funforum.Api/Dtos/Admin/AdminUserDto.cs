namespace Funforum.Api.Dtos.Admin;

public sealed class AdminUserDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public short? BirthYear { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string Status { get; init; } = string.Empty; // X.4
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
