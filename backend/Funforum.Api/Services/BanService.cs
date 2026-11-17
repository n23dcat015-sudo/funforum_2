using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.4 — ban/unban. Trạng thái tài khoản đổi ở đây; KHÔNG thu hồi token đang có
// (user bị ban vẫn đăng nhập được, chỉ bị chặn POST — xem BanCheckMiddleware).
public sealed class BanService(FunforumDbContext db, IAuditLogger audit) : IBanService
{
    public async Task<PagedResult<BanDto>> ListAsync(
        int page, int pageSize, string? status, int? userId,
        string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Bans.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToLowerInvariant();
            if (!BanStatuses.IsValid(wanted))
            {
                throw new ApiException(400, "Invalid status filter.");
            }
            query = query.Where(b => b.Status == wanted);
        }

        if (userId is int uid)
        {
            query = query.Where(b => b.UserId == uid);
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(b => b.Id) : query.OrderBy(b => b.Id),
            _ => query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var bans = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var userIds = bans.Select(b => b.UserId).Distinct().ToList();
        var usernames = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var adminIds = bans.Where(b => b.BannedByAdminId != null)
            .Select(b => b.BannedByAdminId!.Value).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Admins.Where(a => adminIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Username, ct);

        return new PagedResult<BanDto>
        {
            Items = bans.Select(b => ToDto(
                b,
                usernames.GetValueOrDefault(b.UserId) ?? string.Empty,
                b.BannedByAdminId is int aid ? admins.GetValueOrDefault(aid) : null)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<BanDto> CreateAsync(CreateBanRequest request, int actingAdminId, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length is < 1 || reason.Length > ContentLimits.BanReasonMax)
        {
            throw new ApiException(400, $"Reason must be between 1 and {ContentLimits.BanReasonMax} characters.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new ApiException(404, "User not found.");

        var ip = await ResolveIpAsync(request, ct);

        var previousStatus = user.Status;
        if (user.Status != UserStatuses.BannedPermanently)
        {
            user.Status = UserStatuses.Banned;
        }

        var ban = new Ban
        {
            UserId = user.Id,
            BannedIp = ip,
            Reason = reason,
            BannedByAdminId = actingAdminId,
            Status = BanStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Bans.Add(ban);

        // Ghi log nguyên tử cùng lúc với việc tạo ban (AuditLogger không tự commit).
        audit.Write(
            actingAdminId, AuditActions.Ban, AuditTargetTypes.User, user.Id,
            new { status = previousStatus, bannedIp = ip, reason }, reason);

        await db.SaveChangesAsync(ct);
        return await BuildDtoAsync(ban, ct);
    }

    public async Task<BanDto> UnbanAsync(long banId, int actingAdminId, CancellationToken ct)
    {
        var ban = await db.Bans.SingleOrDefaultAsync(b => b.Id == banId, ct)
            ?? throw new ApiException(404, "Ban not found.");
        if (ban.Status == BanStatuses.Lifted)
        {
            throw new ApiException(409, "This ban is already lifted.");
        }

        ban.Status = BanStatuses.Lifted;

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == ban.UserId, ct);
        var previousStatus = user?.Status;
        if (user is not null && user.Status != UserStatuses.BannedPermanently)
        {
            // Chỉ mở lại khi không còn ban active nào khác trên user này.
            var hasOtherActive = await db.Bans.AnyAsync(
                b => b.UserId == user.Id && b.Status == BanStatuses.Active && b.Id != ban.Id, ct);
            if (!hasOtherActive)
            {
                user.Status = UserStatuses.Active;
            }
        }

        audit.Write(
            actingAdminId, AuditActions.Unban, AuditTargetTypes.User, ban.UserId,
            new { status = previousStatus, banId = ban.Id });

        await db.SaveChangesAsync(ct);
        return await BuildDtoAsync(ban, ct);
    }

    public async Task<bool> IsBlockedAsync(int userId, string? ip, CancellationToken ct)
    {
        var active = db.Bans.AsNoTracking().Where(b => b.Status == BanStatuses.Active);
        if (await active.AnyAsync(b => b.UserId == userId, ct))
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(ip) && await active.AnyAsync(b => b.BannedIp == ip, ct))
        {
            return true;
        }
        return false;
    }

    public Task<bool> IsIpBannedAsync(string ip, CancellationToken ct) =>
        db.Bans.AsNoTracking()
            .AnyAsync(b => b.Status == BanStatuses.Active && b.BannedIp == ip, ct);

    private async Task<string?> ResolveIpAsync(CreateBanRequest request, CancellationToken ct)
    {
        string? ip = null;

        if (request.ThreadId is int threadId)
        {
            var thread = await db.Threads.AsNoTracking()
                .Where(t => t.Id == threadId)
                .Select(t => new { t.PosterIp })
                .SingleOrDefaultAsync(ct)
                ?? throw new ApiException(404, "Reported thread not found.");
            ip = thread.PosterIp;
        }
        else if (request.ReplyId is int replyId)
        {
            var reply = await db.Replies.AsNoTracking()
                .Where(r => r.Id == replyId)
                .Select(r => new { r.PosterIp })
                .SingleOrDefaultAsync(ct)
                ?? throw new ApiException(404, "Reported reply not found.");
            ip = reply.PosterIp;
        }
        else if (!string.IsNullOrWhiteSpace(request.BannedIp))
        {
            ip = request.BannedIp.Trim();
        }

        if (ip is { Length: > 45 })
        {
            throw new ApiException(400, "IP address is too long.");
        }

        return ip;
    }

    private async Task<BanDto> BuildDtoAsync(Ban ban, CancellationToken ct)
    {
        var username = await db.Users.Where(u => u.Id == ban.UserId)
            .Select(u => u.Username).SingleOrDefaultAsync(ct) ?? string.Empty;
        var bannerUsername = ban.BannedByAdminId is int aid
            ? await db.Admins.Where(a => a.Id == aid).Select(a => a.Username).SingleOrDefaultAsync(ct)
            : null;
        return ToDto(ban, username, bannerUsername);
    }

    private static BanDto ToDto(Ban ban, string username, string? bannerUsername) => new()
    {
        Id = ban.Id,
        UserId = ban.UserId,
        Username = username,
        BannedIp = ban.BannedIp,
        Reason = ban.Reason,
        BannedByAdminId = ban.BannedByAdminId,
        BannedByUsername = bannerUsername,
        Status = ban.Status,
        CreatedAt = ban.CreatedAt,
    };
}
