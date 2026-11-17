namespace Funforum.Api.Entities;

// Bảng "threads" (tên lấy từ DbSet "Threads", xem FunforumDbContext) — current_state.md
// mục 3.2. Class đặt tên "ForumThread" để tránh trùng System.Threading.Thread (implicit
// using của Microsoft.NET.Sdk.Web).
public class ForumThread
{
    public int Id { get; set; }

    public int BoardId { get; set; }

    public int? AuthorUserId { get; set; }
    public int? AuthorAdminId { get; set; }
    public string? Capcode { get; set; } // "admin" | "superadmin"; NULL = không dùng capcode

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "visible"; // "visible" | "hidden" — ẩn/hiện làm ở GĐ4

    public string? PosterIp { get; set; } // X.4 — IP lúc đăng bài, dùng khi tạo ban

    public int Score { get; set; } // GĐ3b
    public double HotRank { get; set; } // GĐ3c
    public int ReplyCount { get; set; } // Cache: số reply hiển thị công khai
    public int ImageCount { get; set; } // GĐ3d/3f
    public int ImagesPosted { get; set; } // GĐ3d/3g — không giảm khi xóa ảnh

    public DateTime LastReplyAt { get; set; } // = CreatedAt lúc tạo; nâng cấp đầy đủ ở GĐ3f
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}