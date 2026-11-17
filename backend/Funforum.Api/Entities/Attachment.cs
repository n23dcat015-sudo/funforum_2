namespace Funforum.Api.Entities;

// Bảng "attachments" — current_state.md mục 3.7.
public class Attachment
{
    public long Id { get; set; }

    public int? ThreadId { get; set; }
    public int? ReplyId { get; set; }

    public string StoredName { get; set; } = string.Empty; // GUID + đuôi thật
    public string ThumbName { get; set; } = string.Empty; // luôn .jpg
    public string OriginalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty; // image/jpeg | image/png | image/gif
    public int SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public DateTime CreatedAt { get; set; }
}