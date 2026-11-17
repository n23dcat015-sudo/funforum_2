using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Attachments;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public sealed class AdminThreadService(
    FunforumDbContext db,
    IThreadService threadService,
    IAuditLogger audit,
    ILogger<AdminThreadService> logger) : IAdminThreadService
{
    public async Task<PagedResult<AdminThreadDto>> ListAsync(
        int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);
        var query = db.Threads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Content.Contains(term));
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(t => t.Id) : query.OrderBy(t => t.Id),
            "title" => descending ? query.OrderByDescending(t => t.Title).ThenByDescending(t => t.Id) : query.OrderBy(t => t.Title).ThenBy(t => t.Id),
            "replycount" => descending ? query.OrderByDescending(t => t.ReplyCount).ThenByDescending(t => t.Id) : query.OrderBy(t => t.ReplyCount).ThenBy(t => t.Id),
            "score" => descending ? query.OrderByDescending(t => t.Score).ThenByDescending(t => t.Id) : query.OrderBy(t => t.Score).ThenBy(t => t.Id),
            "status" => descending ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.Id) : query.OrderBy(t => t.Status).ThenBy(t => t.Id),
            "createdat" => descending ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id) : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
            "lastreplyat" => descending ? query.OrderByDescending(t => t.LastReplyAt).ThenByDescending(t => t.Id) : query.OrderBy(t => t.LastReplyAt).ThenBy(t => t.Id),
            _ => descending ? query.OrderByDescending(t => t.LastReplyAt).ThenByDescending(t => t.Id) : query.OrderBy(t => t.LastReplyAt).ThenBy(t => t.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var threads = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<AdminThreadDto>
        {
            Items = await MapAsync(threads, ct),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AdminThreadDto> GetAsync(int id, CancellationToken ct) =>
        (await MapAsync([await db.Threads.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ApiException(404, "Thread not found.")], ct)).Single();

    public async Task<ThreadDto> CreateAsync(
        CreateAdminThreadRequest request,
        System.Security.Claims.ClaimsPrincipal admin,
        CancellationToken ct)
    {
        var dto = await threadService.CreateAsync(
            request.BoardSlug,
            new CreateThreadRequest
            {
                Title = request.Title,
                Content = request.Content,
                UseCapcode = request.UseCapcode,
                Image = request.Image,
            },
            admin,
            null, // X.4 — thread do admin tạo không ghi poster_ip
            ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", GetAdminId(admin), "create", "thread", dto.Id);
        return dto;
    }

    public async Task<ThreadDto> UpdateAsync(int id, UpdateThreadRequest request, System.Security.Claims.ClaimsPrincipal admin, CancellationToken ct)
    {
        var dto = await threadService.UpdateAsync(id, request, admin, ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", GetAdminId(admin), "update", "thread", id);
        return dto;
    }

    public async Task DeleteAsync(int id, System.Security.Claims.ClaimsPrincipal admin, CancellationToken ct)
    {
        var adminId = GetAdminId(admin);
        // Đọc trước trạng thái vì threadService.DeleteAsync xoá và commit bên trong.
        var prior = await db.Threads.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new { t.Title, t.Status })
            .SingleOrDefaultAsync(ct);

        await threadService.DeleteAsync(id, admin, ct);

        object? metadata = prior is null ? null : new { prior.Title, prior.Status };
        audit.Write(adminId, AuditActions.Delete, AuditTargetTypes.Thread, id, metadata: metadata);
        await audit.FlushAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "delete", "thread", id);
    }

    public async Task<AdminThreadDto> SetStatusAsync(int id, string status, int adminId, CancellationToken ct)
    {
        if (status is not ("visible" or "hidden"))
        {
            throw new ApiException(400, "Status must be 'visible' or 'hidden'.");
        }

        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ApiException(404, "Thread not found.");
        var previousStatus = thread.Status;
        thread.Status = status;
        thread.UpdatedAt = DateTime.UtcNow;
        // Stage trước SaveChangesAsync để log commit nguyên tử cùng hành động ẩn/hiện.
        audit.Write(
            adminId,
            status == "hidden" ? AuditActions.Hide : AuditActions.Unhide,
            AuditTargetTypes.Thread,
            id,
            metadata: new { status = previousStatus });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, status == "hidden" ? "hide" : "unhide", "thread", id);
        return await GetAsync(id, ct);
    }

    private async Task<List<AdminThreadDto>> MapAsync(List<ForumThread> threads, CancellationToken ct)
    {
        var boardIds = threads.Select(t => t.BoardId).Distinct().ToList();
        var boards = await db.Boards.Where(b => boardIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Slug, ct);
        var userIds = threads.Where(t => t.AuthorUserId != null).Select(t => t.AuthorUserId!.Value).Distinct().ToList();
        var adminIds = threads.Where(t => t.AuthorAdminId != null).Select(t => t.AuthorAdminId!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        var admins = await db.Admins.Where(a => adminIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Username, ct);
        var threadIds = threads.Select(t => t.Id).ToList();
        var images = await db.Attachments.Where(a => a.ThreadId != null && threadIds.Contains(a.ThreadId.Value))
            .ToDictionaryAsync(a => a.ThreadId!.Value, a => a, ct);

        return threads.Select(t => new AdminThreadDto
        {
            Id = t.Id,
            BoardId = t.BoardId,
            BoardSlug = boards.GetValueOrDefault(t.BoardId, string.Empty),
            Title = t.Title,
            Content = t.Content,
            Status = t.Status,
            AuthorUserId = t.AuthorUserId,
            AuthorAdminId = t.AuthorAdminId,
            AuthorUsername = t.AuthorUserId is int uid ? users.GetValueOrDefault(uid) : t.AuthorAdminId is int aid ? admins.GetValueOrDefault(aid) : "[deleted]",
            Capcode = t.Capcode,
            ReplyCount = t.ReplyCount,
            Score = t.Score,
            ImageCount = t.ImageCount,
            Image = images.TryGetValue(t.Id, out var image) ? AttachmentMapper.ToDto(image) : null,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            LastReplyAt = t.LastReplyAt,
        }).ToList();
    }

    private static int GetAdminId(System.Security.Claims.ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirst("sub")!.Value);
}
