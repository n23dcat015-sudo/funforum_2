using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Notifications;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ContentLimits.DefaultPageSize,
        CancellationToken ct = default) =>
        Ok(await notificationService.ListAsync(User, page, pageSize, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new UnreadCountDto { Count = await notificationService.GetUnreadCountAsync(User, ct) });

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notificationService.MarkReadAsync(id, User, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notificationService.MarkAllReadAsync(User, ct);
        return NoContent();
    }
}