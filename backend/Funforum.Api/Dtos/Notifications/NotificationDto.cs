namespace Funforum.Api.Dtos.Notifications;

public class NotificationDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty; // tác giả reply gây ra thông báo
    public string? Capcode { get; set; }
    public int ThreadId { get; set; }
    public int ReplyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}