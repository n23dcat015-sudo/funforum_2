using System.Security.Claims;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Notifications;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public interface INotificationService
{
    // Gọi từ ReplyService.AddAsync ngay sau khi lưu reply thành công (mục 10).
    Task NotifyOnReplyAsync(Reply reply, ForumThread thread, CancellationToken ct);

    Task<PagedResult<NotificationDto>> ListAsync(ClaimsPrincipal user, int page, int pageSize, CancellationToken ct);

    Task<int> GetUnreadCountAsync(ClaimsPrincipal user, CancellationToken ct);

    Task MarkReadAsync(long id, ClaimsPrincipal user, CancellationToken ct);

    Task MarkAllReadAsync(ClaimsPrincipal user, CancellationToken ct);
}