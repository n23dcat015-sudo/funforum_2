using Microsoft.AspNetCore.Http;

namespace Funforum.Api.Dtos.Replies;

public class CreateReplyRequest
{
    public string? Content { get; set; }
    public int? ParentReplyId { get; set; }
    public bool UseCapcode { get; set; }
    public IFormFile? Image { get; set; } // GĐ3d, tùy chọn
}