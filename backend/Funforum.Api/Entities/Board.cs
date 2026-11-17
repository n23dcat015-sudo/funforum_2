namespace Funforum.Api.Entities;

// Bảng "boards" — current_state.md mục 3.2, 3.5.
public class Board
{
    public int Id { get; set; }

    // Không đổi được sau khi tạo (mục 3.5); admin tự gõ, server chỉ kiểm tra định dạng.
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}