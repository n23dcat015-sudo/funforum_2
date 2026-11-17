namespace Funforum.Api.Common;

// current_state.md mục 3.7: thư mục upload cấu hình qua UPLOADS_PATH, gồm 2 thư mục con
// images/ và thumbs/. Trong Docker (GĐ9) đây là named volume gắn vào backend (ghi) và
// Nginx (chỉ đọc).
public class UploadPaths
{
    public string Root { get; }
    public string ImagesDir { get; }
    public string ThumbsDir { get; }

    public UploadPaths(string root)
    {
        Root = Path.GetFullPath(root);
        ImagesDir = Path.Combine(Root, "images");
        ThumbsDir = Path.Combine(Root, "thumbs");
        Directory.CreateDirectory(ImagesDir);
        Directory.CreateDirectory(ThumbsDir);
    }

    public string ImagePath(string storedName) => Path.Combine(ImagesDir, storedName);
    public string ThumbPath(string thumbName) => Path.Combine(ThumbsDir, thumbName);
}