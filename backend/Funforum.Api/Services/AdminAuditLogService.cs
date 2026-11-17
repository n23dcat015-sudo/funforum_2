using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

// X.2 — đọc audit log (chỉ Super Admin, xem AdminAuditLogsController).
public sealed class AdminAuditLogService(FunforumDbContext db) : IAdminAuditLogService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(
        int page, int pageSize, int? actorId, string? action,
        DateTime? from, DateTime? to, string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.AuditLogs.AsNoTracking();

        if (actorId is int aid)
        {
            query = query.Where(l => l.ActorAdminId == aid);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            var wanted = action.Trim();
            query = query.Where(l => l.Action == wanted);
        }
        if (from is DateTime f)
        {
            query = query.Where(l => l.CreatedAt >= f);
        }
        if (to is DateTime t)
        {
            query = query.Where(l => l.CreatedAt <= t);
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(l => l.Id) : query.OrderBy(l => l.Id),
            "action" => descending ? query.OrderByDescending(l => l.Action).ThenByDescending(l => l.Id) : query.OrderBy(l => l.Action).ThenBy(l => l.Id),
            "createdat" => descending ? query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id) : query.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id),
            // Mặc định mới nhất trước: audit log là dòng thời gian.
            _ => query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var actorIds = logs.Where(l => l.ActorAdminId != null)
            .Select(l => l.ActorAdminId!.Value).Distinct().ToList();
        var actors = actorIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Admins.Where(a => actorIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Username, ct);

        return new PagedResult<AuditLogDto>
        {
            Items = logs.Select(l => ToDto(l, actors)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    private static AuditLogDto ToDto(AuditLog log, IReadOnlyDictionary<int, string> actors) => new()
    {
        Id = log.Id,
        ActorAdminId = log.ActorAdminId,
        ActorUsername = log.ActorAdminId is int aid ? actors.GetValueOrDefault(aid) : null,
        Action = log.Action,
        TargetType = log.TargetType,
        TargetId = log.TargetId,
        Reason = log.Reason,
        Metadata = log.Metadata is null ? null : JsonSerializer.Deserialize<JsonElement>(log.Metadata),
        CreatedAt = log.CreatedAt,
    };
}
