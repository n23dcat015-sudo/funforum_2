namespace Funforum.Api.Dtos.Admin;

public sealed class BanDto
{
    public long Id { get; init; }
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? BannedIp { get; init; }
    public string? Reason { get; init; }
    public int? BannedByAdminId { get; init; }
    public string? BannedByUsername { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateBanRequest
{
    public int UserId { get; init; }

    public string Reason { get; set; } = string.Empty;

    // IP lấy từ bài bị report (thread/reply). Ưu tiên hơn BannedIp nếu cùng gửi.
    public int? ThreadId { get; set; }
    public int? ReplyId { get; set; }

    // Dùng khi không có bài để lấy IP.
    public string? BannedIp { get; set; }
}
