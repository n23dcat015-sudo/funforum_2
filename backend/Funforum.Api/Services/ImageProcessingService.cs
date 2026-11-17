using Microsoft.AspNetCore.Http;
using Funforum.Api.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace Funforum.Api.Services;

// current_state.md mục 3.7 "Xử lý phía server khi nhận ảnh".
public class ImageProcessingService(UploadPaths paths) : IImageProcessingService
{
    private const int MaxFileBytes = 4 * 1024 * 1024;
    private const int MaxDimension = 6000;
    private const int ThumbMaxSide = 250;

    public async Task<ProcessedImage> ProcessAndSaveAsync(IFormFile file, CancellationToken ct)
    {
        // Bước 3 (một phần): kiểm tra dung lượng trước, chưa đụng tới nội dung file.
        if (file.Length is 0 or > MaxFileBytes)
        {
            throw new ApiException(400, $"Image must be at most {MaxFileBytes / 1024 / 1024} MB.");
        }

        await using var input = file.OpenReadStream();

        // Bước 2: nhận dạng định dạng thật bằng vài byte đầu — bỏ qua đuôi file và
        // Content-Type do client gửi (mục 11.6).
        IImageFormat? format;
        try
        {
            format = await Image.DetectFormatAsync(input, ct);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            throw new ApiException(400, "Not a valid image file.");
        }
        if (format is null)
        {
            throw new ApiException(400, "Not a valid image file.");
        }

        var (mimeType, extension) = format.Name switch
        {
            "JPEG" => ("image/jpeg", ".jpg"),
            "PNG" => ("image/png", ".png"),
            "GIF" => ("image/gif", ".gif"),
            _ => throw new ApiException(400, "Only JPEG, PNG, or GIF images are accepted."),
        };

        // Bước 3: đọc kích thước từ header (không giải mã toàn bộ) để từ chối ảnh quá lớn
        // trước khi tốn RAM.
        input.Position = 0;
        ImageInfo? info;
        try
        {
            info = await Image.IdentifyAsync(input, ct);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            throw new ApiException(400, "Not a valid image file.");
        }
        if (info is null)
        {
            throw new ApiException(400, "Not a valid image file.");
        }
        if (info.Width > MaxDimension || info.Height > MaxDimension)
        {
            throw new ApiException(400, $"Image dimensions must be at most {MaxDimension}x{MaxDimension} px.");
        }

        // Bước 4: giải mã rồi lưu lại thành file mới (bỏ EXIF/ICC) thay vì lưu nguyên file gốc.
        input.Position = 0;
        Image image;
        try
        {
            image = await Image.LoadAsync(input, ct);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            throw new ApiException(400, "Not a valid image file.");
        }
        using var _ = image;

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var thumbName = $"{Guid.NewGuid():N}.jpg";

        try
        {
            await SaveOriginalAsync(image, format, paths.ImagePath(storedName), ct);
            await SaveThumbnailAsync(image, paths.ThumbPath(thumbName), ct);
        }
        catch
        {
            DeleteFiles(storedName, thumbName);
            throw;
        }

        var sizeBytes = (int)new FileInfo(paths.ImagePath(storedName)).Length;
        var originalName = SanitizeOriginalName(file.FileName);

        return new ProcessedImage(storedName, thumbName, originalName, mimeType, sizeBytes, image.Width, image.Height);
    }

    public void DeleteFiles(string storedName, string thumbName)
    {
        TryDelete(paths.ImagePath(storedName));
        TryDelete(paths.ThumbPath(thumbName));
    }

    private static async Task SaveOriginalAsync(Image image, IImageFormat format, string path, CancellationToken ct)
    {
        await using var output = File.Create(path);
        switch (format.Name)
        {
            case "JPEG":
                await image.SaveAsJpegAsync(output, ct);
                break;
            case "PNG":
                await image.SaveAsPngAsync(output, ct);
                break;
            case "GIF":
                // Load + SaveAsGif giữ nguyên toàn bộ frame (mục 3.7: "GIF động giữ nguyên").
                await image.SaveAsGifAsync(output, ct);
                break;
        }
    }

    private static async Task SaveThumbnailAsync(Image image, string path, CancellationToken ct)
    {
        // Bước 5: JPEG, cạnh dài tối đa 250px, GIF động lấy khung đầu, nền trong suốt
        // thay bằng nền trắng.
        using var thumb = image.Frames.CloneFrame(0);
        thumb.Mutate(x => x
            .BackgroundColor(Color.White)
            .Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(ThumbMaxSide, ThumbMaxSide) }));

        await using var output = File.Create(path);
        await thumb.SaveAsJpegAsync(output, ct);
    }

    private static string SanitizeOriginalName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        return name.Length > 255 ? name[..255] : name;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Job dọn file mồ côi (OrphanFileCleanupService) sẽ dọn lại sau nếu xóa lỗi ở đây.
        }
    }
}