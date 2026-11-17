using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Reports;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.3 — tạo report, KHÔNG tạo cờ ẩn/xóa riêng: việc ẩn/xóa vẫn dùng endpoint cũ.
public sealed class ReportService(FunforumDbContext db) : IReportService
{
    public async Task<long> CreateAsync(CreateReportRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var targetType = (request.TargetType ?? string.Empty).Trim().ToLowerInvariant();
        if (!ReportTargetTypes.IsValid(targetType))
        {
            throw new ApiException(400, "Target type must be 'thread' or 'reply'.");
        }

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length is < 1 || reason.Length > ContentLimits.ReportReasonMax)
        {
            throw new ApiException(400, $"Reason must be between 1 and {ContentLimits.ReportReasonMax} characters.");
        }

        var exists = targetType == ReportTargetTypes.Thread
            ? await db.Threads.Visible().AnyAsync(t => t.Id == request.TargetId, ct)
            : await db.Replies.AnyAsync(r => r.Id == request.TargetId && r.Status == "visible", ct);
        if (!exists)
        {
            throw new ApiException(404, "Reported content was not found.");
        }

        var report = new Report
        {
            TargetType = targetType,
            TargetId = request.TargetId,
            // Chỉ gắn người report khi là user thường; admin (nếu gọi) để null.
            ReporterUserId = Roles.Of(user) == Roles.User ? AuthorContext.Resolve(user).UserId : null,
            Reason = reason,
            Status = ReportStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return report.Id;
    }
}
