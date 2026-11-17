namespace Funforum.Api.Entities;

// Bảng "notifications" — current_state.md mục 3.2, 10.
public class Notification
{
    public long Id { get; set; }

    public int? RecipientUserId { get; set; }
    public int? RecipientAdminId { get; set; }

    public int ReplyId { get; set; } // Reply gây ra thông báo

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}