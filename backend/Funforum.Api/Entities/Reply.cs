namespace Funforum.Api.Entities;

// Bảng "replies" — current_state.md mục 3.2, 3.3.
public class Reply
{
    public int Id { get; set; }

    public int ThreadId { get; set; }

    // NULL = reply gốc; khác NULL = trả lời đúng reply đó ở cấp thật bất kỳ.
    public int? ParentReplyId { get; set; }
    // NULL nếu chính nó là reply gốc; dùng để gom 2 cấp hiển thị kiểu Instagram (mục 7.4,
    // xử lý đầy đủ ở GĐ3e).
    public int? RootReplyId { get; set; }

    public int? AuthorUserId { get; set; }
    public int? AuthorAdminId { get; set; }
    public string? Capcode { get; set; }

    public string Content { get; set; } = string.Empty; // Có thể rỗng nếu có ảnh (GĐ3d)

    public string Status { get; set; } = "visible"; // Ẩn/hiện đệ quy làm đầy đủ ở GĐ3e
    public string? HiddenBy { get; set; } // "owner" | "admin"
    public int HiddenAncestorCount { get; set; }

    public string? PosterIp { get; set; } // X.4 — IP lúc đăng bài, dùng khi tạo ban

    public int ChildCount { get; set; } // Cache: số reply cấp dưới đang hiển thị (GĐ3e)
    public int Score { get; set; } // GĐ3b

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}