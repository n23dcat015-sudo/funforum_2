using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.3 — escalate/resolve KHÔNG đụng tới trạng thái ẩn/hiện của nội dung; Janitor vẫn
// dùng endpoint ẩn/xóa cũ để xử lý nội dung.
public sealed class AdminReportService(FunforumDbContext db) : IAdminReportService
{
    public async Task<PagedResult<AdminReportDto>> ListAsync(
        int page, int pageSize, string? status, string? targetType,
        string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Reports.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToLowerInvariant();
            if (!ReportStatuses.IsValid(wanted))
            {
                throw new ApiException(400, "Invalid status filter.");
            }
            query = query.Where(r => r.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            var wanted = targetType.Trim().ToLowerInvariant();
            if (!ReportTargetTypes.IsValid(wanted))
            {
                throw new ApiException(400, "Invalid target type filter.");
            }
            query = query.Where(r => r.TargetType == wanted);
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(r => r.Id) : query.OrderBy(r => r.Id),
            "createdat" => descending
                ? query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                : query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            // Mặc định: report mới nhất trước.
            _ => query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var reports = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var reporterIds = reports.Where(r => r.ReporterUserId != null)
            .Select(r => r.ReporterUserId!.Value).Distinct().ToList();
        var reporters = reporterIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Users.Where(u => reporterIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        return new PagedResult<AdminReportDto>
        {
            Items = reports.Select(r => ToDto(r, reporters)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AdminReportDto> EscalateAsync(long id, CancellationToken ct)
    {
        var report = await db.Reports.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Report not found.");
        if (report.Status == ReportStatuses.Resolved)
        {
            throw new ApiException(409, "This report is already resolved.");
        }

        report.Status = ReportStatuses.Escalated;
        await db.SaveChangesAsync(ct);
        return await BuildDtoAsync(report, ct);
    }

    public async Task<AdminReportDto> ResolveAsync(long id, CancellationToken ct)
    {
        var report = await db.Reports.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Report not found.");
        if (report.Status == ReportStatuses.Resolved)
        {
            throw new ApiException(409, "This report is already resolved.");
        }

        report.Status = ReportStatuses.Resolved;
        await db.SaveChangesAsync(ct);
        return await BuildDtoAsync(report, ct);
    }

    private async Task<AdminReportDto> BuildDtoAsync(Report report, CancellationToken ct)
    {
        var reporters = report.ReporterUserId is int uid
            ? await db.Users.Where(u => u.Id == uid).ToDictionaryAsync(u => u.Id, u => u.Username, ct)
            : new Dictionary<int, string>();
        return ToDto(report, reporters);
    }

    private static AdminReportDto ToDto(Report report, IReadOnlyDictionary<int, string> reporters) => new()
    {
        Id = report.Id,
        TargetType = report.TargetType,
        TargetId = report.TargetId,
        ReporterUserId = report.ReporterUserId,
        ReporterUsername = report.ReporterUserId is int uid ? reporters.GetValueOrDefault(uid) : null,
        Reason = report.Reason,
        Status = report.Status,
        CreatedAt = report.CreatedAt,
    };
}
