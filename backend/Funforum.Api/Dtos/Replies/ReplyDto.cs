using Funforum.Api.Dtos.Attachments;

namespace Funforum.Api.Dtos.Replies;

public class ReplyDto
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int? ParentReplyId { get; set; }
    public int? RootReplyId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;
    public string? Capcode { get; set; }

    // Phase A: cờ sở hữu tính sẵn theo người gọi (false với khách chưa đăng nhập).
    // CanDelete gồm cả trường hợp người gọi là chủ thread (mục 3.3).
    public bool IsMine { get; set; }
    public bool CanDelete { get; set; }

    public int ChildCount { get; set; }
    public int Score { get; set; }
    public string Status { get; set; } = "visible";
    public string? HiddenBy { get; set; }
    public bool IsHidden { get; set; }
    public AttachmentDto? Image { get; set; }

    // mục 7.4: @username của reply cấp 2 khi người được trả lời khác tác giả reply gốc
    // của nhánh. Tính lúc hiển thị, không lưu ở DB.
    public string? ReplyToUsername { get; set; }

    // mục 7.4: chỉ có ở reply gốc, trả bởi GET /threads/{id}/replies — vài reply cấp 2
    // đầu mỗi nhánh. Null với các DTO khác (kể cả reply cấp 2 lấy từ /replies/{id}/children).
    public IReadOnlyList<ReplyDto>? PreviewChildren { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}