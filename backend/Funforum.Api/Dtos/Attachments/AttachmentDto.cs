namespace Funforum.Api.Dtos.Attachments;

public class AttachmentDto
{
    public string Url { get; set; } = string.Empty;
    public string ThumbUrl { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int SizeBytes { get; set; }
}