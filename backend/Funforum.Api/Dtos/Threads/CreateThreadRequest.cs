using Microsoft.AspNetCore.Http;

namespace Funforum.Api.Dtos.Threads;

public class CreateThreadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool UseCapcode { get; set; }
    public IFormFile? Image { get; set; } // GĐ3d, tùy chọn
}