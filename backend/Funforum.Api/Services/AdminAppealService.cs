using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.5 — hàng chờ kháng cáo cho Moderator+. Approve mở ban ngay; reject giữ nguyên và
// khóa việc nộp lại (AppealService.Create chặn đơn pending/rejected cùng ban).
public sealed class AdminAppealService(FunforumDbContext db, IAuditLogger audit) : IAdminAppealService
{
    public async Task<PagedResult<AppealDto>> ListAsync(
        int page, int pageSize, string? status, int? userId,
        string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.BanAppeals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToLowerInvariant();
            if (!AppealStatuses.IsValid(wanted))
            {
                throw new ApiException(400, "Invalid status filter.");
            }
            query = query.Where(a => a.Status == wanted);
        }

        if (userId is int uid)
        {
            query = query.Where(a => a.UserId == uid);
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(a => a.Id) : query.OrderBy(a => a.Id),
            _ => query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var appeals = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var userIds = appeals.Select(a => a.UserId).Distinct().ToList();
        var usernames = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var banIds = appeals.Select(a => a.BanId).Distinct().ToList();
        var ips = banIds.Count == 0
            ? new Dictionary<long, string?>()
            : await db.Bans.Where(b => banIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.BannedIp, ct);

        var adminIds = appeals.Where(a => a.ReviewedByAdminId != null)
            .Select(a => a.ReviewedByAdminId!.Value).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Admins.Where(a => adminIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Username, ct);

        return new PagedResult<AppealDto>
        {
            Items = appeals.Select(a => ToDto(
                a,
                usernames.GetValueOrDefault(a.UserId) ?? string.Empty,
                ips.GetValueOrDefault(a.BanId),
                a.ReviewedByAdminId is int aid ? admins.GetValueOrDefault(aid) : null)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AppealDto> ReviewAsync(long id, string? decision, int actingAdminId, CancellationToken ct)
    {
        var wanted = (decision ?? string.Empty).Trim().ToLowerInvariant();
        if (!AppealDecisions.IsValid(wanted))
        {
            throw new ApiException(400, "Decision must be 'approve' or 'reject'.");
        }

        var appeal = await db.BanAppeals.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ApiException(404, "Appeal not found.");
        if (appeal.Status != AppealStatuses.Pending)
        {
            throw new ApiException(409, "This appeal has already been reviewed.");
        }

        if (wanted == AppealDecisions.Approve)
        {
            var ban = await db.Bans.SingleOrDefaultAsync(b => b.Id == appeal.BanId, ct);
            if (ban is not null)
            {
                ban.Status = BanStatuses.Lifted;
            }

            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == appeal.UserId, ct);
            if (user is not null)
            {
                user.Status = UserStatuses.Active;
                user.UpdatedAt = DateTime.UtcNow;
            }

            appeal.Status = AppealStatuses.Approved;
            audit.Write(
                actingAdminId, AuditActions.Unban, AuditTargetTypes.User, appeal.UserId,
                new { banId = appeal.BanId, appealId = appeal.Id, source = "appeal" },
                "Appeal approved");
        }
        else
        {
            appeal.Status = AppealStatuses.Rejected;
        }

        appeal.ReviewedByAdminId = actingAdminId;
        appeal.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await BuildDtoAsync(appeal, ct);
    }

    private async Task<AppealDto> BuildDtoAsync(BanAppeal appeal, CancellationToken ct)
    {
        var username = await db.Users.Where(u => u.Id == appeal.UserId)
            .Select(u => u.Username).SingleOrDefaultAsync(ct) ?? string.Empty;
        var ip = await db.Bans.Where(b => b.Id == appeal.BanId)
            .Select(b => b.BannedIp).SingleOrDefaultAsync(ct);
        var reviewer = appeal.ReviewedByAdminId is int aid
            ? await db.Admins.Where(a => a.Id == aid).Select(a => a.Username).SingleOrDefaultAsync(ct)
            : null;
        return ToDto(appeal, username, ip, reviewer);
    }

    private static AppealDto ToDto(BanAppeal appeal, string username, string? bannedIp, string? reviewerUsername) => new()
    {
        Id = appeal.Id,
        BanId = appeal.BanId,
        UserId = appeal.UserId,
        Username = username,
        BannedIp = bannedIp,
        ReasonText = appeal.ReasonText,
        Status = appeal.Status,
        ReviewedByAdminId = appeal.ReviewedByAdminId,
        ReviewedByUsername = reviewerUsername,
        CreatedAt = appeal.CreatedAt,
        ReviewedAt = appeal.ReviewedAt,
    };
}
