using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Notifications;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public class NotificationService(FunforumDbContext db) : INotificationService
{
    public async Task NotifyOnReplyAsync(Reply reply, ForumThread thread, CancellationToken ct)
    {
        int? recipientUserId;
        int? recipientAdminId;

        if (reply.ParentReplyId is null)
        {
            // reply gốc (trả lời thẳng vào thread) → chủ thread nhận thông báo.
            recipientUserId = thread.AuthorUserId;
            recipientAdminId = thread.AuthorAdminId;
        }
        else
        {
            // reply cấp 2 (bất kỳ độ sâu thật) → tác giả parent_reply_id nhận thông báo,
            // KHÔNG phải chủ thread (mục 10: "chủ thread không nhận thông báo cho reply cấp 2").
            var parent = await db.Replies
                .Where(r => r.Id == reply.ParentReplyId)
                .Select(r => new { r.AuthorUserId, r.AuthorAdminId })
                .SingleOrDefaultAsync(ct);
            recipientUserId = parent?.AuthorUserId;
            recipientAdminId = parent?.AuthorAdminId;
        }

        // Người nhận không còn tài khoản (đã bị xóa) → bỏ qua, không tạo dòng.
        if (recipientUserId is null && recipientAdminId is null)
        {
            return;
        }

        // Không tự thông báo cho chính người đăng.
        if (recipientUserId == reply.AuthorUserId && recipientAdminId == reply.AuthorAdminId)
        {
            return;
        }

        db.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId,
            RecipientAdminId = recipientAdminId,
            ReplyId = reply.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(
        ClaimsPrincipal user, int page, int pageSize, CancellationToken ct)
    {
        var (userId, adminId) = CurrentIdentity(user);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = VisibleNotificationsQuery(userId, adminId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);

        var totalItems = await query.CountAsync(ct);
        var notifications = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<NotificationDto>
        {
            Items = await MapAsync(notifications, ct),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<int> GetUnreadCountAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var (userId, adminId) = CurrentIdentity(user);
        return await VisibleNotificationsQuery(userId, adminId).CountAsync(n => n.ReadAt == null, ct);
    }

    public async Task MarkReadAsync(long id, ClaimsPrincipal user, CancellationToken ct)
    {
        var (userId, adminId) = CurrentIdentity(user);
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new ApiException(404, "Notification not found.");

        if (notification.RecipientUserId != userId || notification.RecipientAdminId != adminId)
        {
            throw new ApiException(403, "This notification does not belong to you.");
        }

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllReadAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var (userId, adminId) = CurrentIdentity(user);
        await db.Notifications
            .Where(n => n.RecipientUserId == userId && n.RecipientAdminId == adminId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }

    // mục 10: lọc bỏ notification của reply đang ẩn/đã xóa (đã xóa thì cascade mất dòng
    // notification, chỉ cần lọc điều kiện hiển thị của reply còn lại).
    private IQueryable<Notification> VisibleNotificationsQuery(int? userId, int? adminId) =>
        db.Notifications
          .Where(n => n.RecipientUserId == userId && n.RecipientAdminId == adminId)
          .Where(n => db.Replies.Any(r => r.Id == n.ReplyId && r.Status == "visible" && r.HiddenAncestorCount == 0));

    private async Task<List<NotificationDto>> MapAsync(List<Notification> notifications, CancellationToken ct)
    {
        var replyIds = notifications.Select(n => n.ReplyId).Distinct().ToList();
        var replies = await db.Replies.Where(r => replyIds.Contains(r.Id))
            .Select(r => new { r.Id, r.ThreadId, r.AuthorUserId, r.AuthorAdminId, r.Capcode })
            .ToDictionaryAsync(r => r.Id, ct);

        var users = await AuthorLookup.GetDisplayNamesAsync(
            db, replies.Values.Where(r => r.AuthorUserId != null).Select(r => r.AuthorUserId!.Value), ct);
        var admins = await AuthorLookup.GetAdminUsernamesAsync(
            db, replies.Values.Where(r => r.AuthorAdminId != null).Select(r => r.AuthorAdminId!.Value), ct);

        return notifications.Select(n =>
        {
            var r = replies[n.ReplyId];
            return new NotificationDto
            {
                Id = n.Id,
                Username = AuthorLookup.ResolvePublicName(r.AuthorUserId, r.AuthorAdminId, r.Capcode, users, admins),
                Capcode = r.Capcode,
                ThreadId = r.ThreadId,
                ReplyId = n.ReplyId,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
            };
        }).ToList();
    }

    private static (int? UserId, int? AdminId) CurrentIdentity(ClaimsPrincipal user)
    {
        var role = Roles.Of(user);
        var subId = int.Parse(user.FindFirst("sub")!.Value);
        return role == Roles.User ? (subId, (int?)null) : ((int?)null, subId);
    }
}