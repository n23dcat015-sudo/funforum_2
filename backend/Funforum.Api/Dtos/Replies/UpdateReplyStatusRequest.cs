namespace Funforum.Api.Dtos.Replies;

public class UpdateReplyStatusRequest
{
    public string Status { get; set; } = string.Empty; // "visible" | "hidden"
}