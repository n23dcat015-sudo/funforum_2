using Funforum.Api.Dtos.Attachments;

namespace Funforum.Api.Dtos.Admin;

public sealed class AdminThreadDto
{
    public int Id { get; init; }
    public int BoardId { get; init; }
    public string BoardSlug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? AuthorUserId { get; init; }
    public int? AuthorAdminId { get; init; }
    public string? AuthorUsername { get; init; }
    public string? Capcode { get; init; }
    public int ReplyCount { get; init; }
    public int Score { get; init; }
    public int ImageCount { get; init; }
    public AttachmentDto? Image { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime LastReplyAt { get; init; }
}
