using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

public sealed class UpdateAdminBoardRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }

    public int SortOrder { get; init; }
}
