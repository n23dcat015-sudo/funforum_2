using Funforum.Api.Dtos.Attachments;
using Funforum.Api.Dtos.Replies;

namespace Funforum.Api.Dtos.Threads;

public class ThreadDto
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string BoardSlug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;
    public string? Capcode { get; set; }

    // Phase A: cờ sở hữu tính sẵn theo người gọi (false với khách chưa đăng nhập).
    public bool IsMine { get; set; }
    public bool CanDelete { get; set; }

    public int ReplyCount { get; set; }
    public int Score { get; set; }
    public AttachmentDto? Image { get; set; }

    public IReadOnlyList<ReplyDto>? PreviewReplies { get; set; }
    public int OmittedReplies { get; set; }
    public int OmittedImages { get; set; }

    public int ImagesPosted { get; set; }
    public bool ImageLimitReached { get; set; }
    public bool ReplyLimitReached { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastReplyAt { get; set; }
}