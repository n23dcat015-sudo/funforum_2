using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

public sealed class UpdateAdminUserRequest
{
    [MaxLength(100)]
    public string? FullName { get; init; }

    public short? BirthYear { get; init; }

    [MaxLength(255)]
    public string? Address { get; init; }

    [MaxLength(20)]
    public string? Phone { get; init; }
}
