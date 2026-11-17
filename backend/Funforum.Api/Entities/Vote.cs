namespace Funforum.Api.Entities;

// Bảng "votes" — current_state.md mục 3.2. Khai báo sẵn ở GĐ3a để migration không phải
// sửa schema giữa chừng; endpoint vote (PUT .../vote) làm ở GĐ3b.
public class Vote
{
    public long Id { get; set; }

    public int UserId { get; set; }
    public int? ThreadId { get; set; }
    public int? ReplyId { get; set; }

    public short Value { get; set; } // CHECK (-1, 1)

    public DateTime CreatedAt { get; set; }
}