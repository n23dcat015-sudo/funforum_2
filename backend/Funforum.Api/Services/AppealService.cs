using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Appeals;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.5 — user bị ban tự nộp đơn kháng cáo cho lệnh ban đang áp dụng lên mình.
public sealed class AppealService(FunforumDbContext db) : IAppealService
{
    public async Task<MyAppealDto> GetMineAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ApiException(404, "User not found.");

        var locked = user.Status == UserStatuses.BannedPermanently;

        var ban = await ActiveBanQuery(userId).FirstOrDefaultAsync(ct);
        if (ban is null)
        {
            return new MyAppealDto { AppealsLocked = locked };
        }

        var appeal = await db.BanAppeals.AsNoTracking()
            .Where(a => a.BanId == ban.Id)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);

        return ToDto(ban, appeal, locked);
    }

    public async Task<MyAppealDto> CreateAsync(CreateAppealRequest request, int userId, CancellationToken ct)
    {
        var reason = (request.ReasonText ?? string.Empty).Trim();
        if (reason.Length is < 1 || reason.Length > ContentLimits.AppealReasonMax)
        {
            throw new ApiException(400, $"Reason must be between 1 and {ContentLimits.AppealReasonMax} characters.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ApiException(404, "User not found.");
        if (user.Status == UserStatuses.BannedPermanently)
        {
            throw new ApiException(403, "Appeals are disabled for this account.");
        }

        var ban = await db.Bans.SingleOrDefaultAsync(b => b.Id == request.BanId && b.UserId == userId, ct)
            ?? throw new ApiException(404, "Ban not found.");
        if (ban.Status != BanStatuses.Active)
        {
            throw new ApiException(409, "This ban is not active.");
        }

        // Chỉ cho nộp khi chưa có đơn pending/rejected cho lệnh ban này.
        var blocked = await db.BanAppeals.AnyAsync(
            a => a.BanId == ban.Id
                 && (a.Status == AppealStatuses.Pending || a.Status == AppealStatuses.Rejected), ct);
        if (blocked)
        {
            throw new ApiException(409, "You already have an open or rejected appeal for this ban.");
        }

        var appeal = new BanAppeal
        {
            BanId = ban.Id,
            UserId = user.Id,
            ReasonText = reason,
            Status = AppealStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.BanAppeals.Add(appeal);
        await db.SaveChangesAsync(ct);

        return ToDto(ban, appeal, locked: false);
    }

    private IQueryable<Ban> ActiveBanQuery(int userId) =>
        db.Bans.AsNoTracking()
            .Where(b => b.UserId == userId && b.Status == BanStatuses.Active)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id);

    private static MyAppealDto ToDto(Ban ban, BanAppeal? appeal, bool locked) => new()
    {
        AppealsLocked = locked,
        BanId = ban.Id,
        BanReason = ban.Reason,
        BannedIp = ban.BannedIp,
        AppealStatus = appeal?.Status,
        AppealReasonText = appeal?.ReasonText,
        AppealCreatedAt = appeal?.CreatedAt,
        AppealReviewedAt = appeal?.ReviewedAt,
        CanSubmit = appeal is null && !locked,
    };
}
