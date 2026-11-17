using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

public sealed class CreateAdminThreadRequest
{
    [Required, MaxLength(50)]
    public string BoardSlug { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(10000)]
    public string Content { get; init; } = string.Empty;

    public bool UseCapcode { get; init; }
    public IFormFile? Image { get; init; }
}
