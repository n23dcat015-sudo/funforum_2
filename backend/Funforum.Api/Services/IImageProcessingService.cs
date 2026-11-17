using Microsoft.AspNetCore.Http;

namespace Funforum.Api.Services;

public record ProcessedImage(
    string StoredName, string ThumbName, string OriginalName,
    string MimeType, int SizeBytes, int Width, int Height);

public interface IImageProcessingService
{
    Task<ProcessedImage> ProcessAndSaveAsync(IFormFile file, CancellationToken ct);

    void DeleteFiles(string storedName, string thumbName);
}