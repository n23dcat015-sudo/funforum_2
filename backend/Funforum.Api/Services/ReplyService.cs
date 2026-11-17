using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Replies;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public class ReplyService(
    FunforumDbContext db,
    IImageProcessingService imageProcessing,
    INotificationService notificationService,
    IAuditLogger audit,
    IOptions<ContentLimitsOptions> contentLimitsOptions) : IReplyService
{
    private readonly ContentLimitsOptions _limits = contentLimitsOptions.Value;

    public async Task<ReplyDto> AddAsync(
        int threadId, CreateReplyRequest request, ClaimsPrincipal user, string? ip, CancellationToken ct)
    {
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == threadId, ct)
            ?? throw new ApiException(404, "Thread not found.");
        if (thread.Status != "visible")
        {
            throw new ApiException(409, "This thread is hidden.");
        }

        Reply? parent = null;
        if (request.ParentReplyId is int parentId)
        {
            parent = await db.Replies.SingleOrDefaultAsync(r => r.Id == parentId && r.ThreadId == threadId, ct)
                ?? throw new ApiException(400, "Invalid parent reply.");
            if (parent.Status != "visible" || parent.HiddenAncestorCount != 0)
            {
                throw new ApiException(409, "Cannot reply to a hidden reply.");
            }
        }

        var content = (request.Content ?? string.Empty).Trim();
        var hasImage = request.Image is { Length: > 0 };

        if (!hasImage && content.Length < 1)
        {
            throw new ApiException(400, "Reply must have content or an image.");
        }
        if (content.Length > ContentLimits.ReplyContentMax)
        {
            throw new ApiException(400, $"Content must be at most {ContentLimits.ReplyContentMax} characters.");
        }

        var author = AuthorContext.Resolve(user);
        var capcode = AuthorContext.ResolveCapcode(author.Role, request.UseCapcode);

        // Ảnh (I/O đĩa) xử lý SAU mọi kiểm tra rẻ tiền, TRƯỚC khi khóa dòng thread — không
        // giữ FOR UPDATE lock trong lúc chờ ghi file.
        ProcessedImage? processed = hasImage
            ? await imageProcessing.ProcessAndSaveAsync(request.Image!, ct)
            : null;

        try
        {
            var now = DateTime.UtcNow;
            var rootReplyId = parent is null ? (int?)null : parent.RootReplyId ?? parent.Id;

            var reply = new Reply
            {
                ThreadId = threadId,
                ParentReplyId = parent?.Id,
                RootReplyId = rootReplyId,
                AuthorUserId = author.UserId,
                AuthorAdminId = author.AdminId,
                Capcode = capcode,
                Content = content,
                Status = "visible",
                PosterIp = ip, // X.4
                CreatedAt = now,
                UpdatedAt = now,
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // current_state.md mục 3.8 "đã chốt": khóa dòng thread bằng SELECT ... FOR UPDATE
            // trước khi đếm/kiểm tra 2 giới hạn, để 2 request đăng reply sát ngưỡng cùng lúc
            // không cùng đọc thấy "còn chỗ" rồi cùng ghi vượt giới hạn. CHƯA chạy thử được ở
            // sandbox này (không có dotnet SDK) — kỹ thuật ExecuteSqlInterpolatedAsync chạy
            // SELECT ... FOR UPDATE (không đọc kết quả) để giữ lock là kỹ thuật chuẩn của
            // Postgres, nhưng bạn nên tự xác nhận khi test 2 request đồng thời thật.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM threads WHERE id = {threadId} FOR UPDATE", ct);
            await db.Entry(thread).ReloadAsync(ct);

            var existingReplyCount = await db.Replies.CountAsync(r => r.ThreadId == threadId, ct);
            if (existingReplyCount >= _limits.MaxRepliesPerThread)
            {
                throw new ApiException(409, "This thread is full.");
            }
            if (hasImage && thread.ImagesPosted >= _limits.MaxImagesPerThread)
            {
                throw new ApiException(409, "Image limit reached.");
            }

            db.Replies.Add(reply);
            thread.ReplyCount += 1;
            thread.LastReplyAt = now;
            if (processed is not null)
            {
                thread.ImagesPosted += 1;
                thread.ImageCount += 1;
            }

            await db.SaveChangesAsync(ct);

            if (processed is not null)
            {
                db.Attachments.Add(new Attachment
                {
                    ReplyId = reply.Id,
                    StoredName = processed.StoredName,
                    ThumbName = processed.ThumbName,
                    OriginalName = processed.OriginalName,
                    MimeType = processed.MimeType,
                    SizeBytes = processed.SizeBytes,
                    Width = processed.Width,
                    Height = processed.Height,
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(ct);
            }

            if (rootReplyId is int rid)
            {
                await db.Replies.Where(r => r.Id == rid)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.ChildCount, r => r.ChildCount + 1), ct);
            }

            await notificationService.NotifyOnReplyAsync(reply, thread, ct);
            await tx.CommitAsync(ct);

            return (await ReplyMapper.MapAsync(db, [reply], ViewerContext.From(user), ct)).Single();
        }
        catch
        {
            if (processed is not null)
            {
                imageProcessing.DeleteFiles(processed.StoredName, processed.ThumbName);
            }
            throw;
        }
    }

    public async Task<ReplyDto> GetByIdAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var reply = await db.Replies.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Reply not found.");
        var thread = await db.Threads.Visible().SingleOrDefaultAsync(t => t.Id == reply.ThreadId, ct)
            ?? throw new ApiException(404, "Reply not found.");
        if (!await ReadableRepliesQuery(thread, user).AnyAsync(r => r.Id == id, ct))
        {
            throw new ApiException(404, "Reply not found.");
        }

        var readableReplies = await ReadableRepliesQuery(thread, user).ToListAsync(ct);
        var counts = CountDescendants(readableReplies);
        var dto = (await ReplyMapper.MapAsync(db, [reply], ViewerContext.From(user), ct)).Single();
        dto.ChildCount = counts.GetValueOrDefault(reply.Id);
        return dto;
    }

    public async Task<PagedResult<ReplyDto>> GetRootRepliesAsync(
        int threadId, int page, int pageSize, ClaimsPrincipal user, CancellationToken ct)
    {
        var thread = await db.Threads.Visible().SingleOrDefaultAsync(t => t.Id == threadId, ct)
            ?? throw new ApiException(404, "Thread not found.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var readableReplies = ReadableRepliesQuery(thread, user);
        var query = readableReplies
            .Where(r => r.ThreadId == threadId && r.ParentReplyId == null)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);

        var totalItems = await query.CountAsync(ct);
        var roots = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var mappedRoots = await ReplyMapper.MapAsync(db, roots, ViewerContext.From(user), ct);

        var rootIds = roots.Select(r => r.Id).ToList();
        if (rootIds.Count > 0)
        {
            var candidates = await ReadableRepliesQuery(thread, user)
                .Where(r => r.RootReplyId != null && rootIds.Contains(r.RootReplyId!.Value))
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .ToListAsync(ct);

            var counts = CountDescendants(roots.Concat(candidates).ToList());
            foreach (var dto in mappedRoots)
            {
                dto.ChildCount = counts.GetValueOrDefault(dto.Id);
            }

            var previewByRoot = candidates
                .GroupBy(r => r.RootReplyId!.Value)
                .ToDictionary(g => g.Key, g => g.Take(ContentLimits.RootReplyPreviewCount).ToList());

            var allPreviews = previewByRoot.Values.SelectMany(v => v).ToList();
            var mappedPreviews = (await ReplyMapper.MapAsync(db, allPreviews, ViewerContext.From(user), ct)).ToDictionary(p => p.Id);
            foreach (var dto in mappedPreviews.Values)
            {
                dto.ChildCount = counts.GetValueOrDefault(dto.Id);
            }

            foreach (var dto in mappedRoots)
            {
                if (previewByRoot.TryGetValue(dto.Id, out var previewEntities))
                {
                    dto.PreviewChildren = previewEntities.Select(p => mappedPreviews[p.Id]).ToList();
                }
            }
        }

        return new PagedResult<ReplyDto>
        {
            Items = mappedRoots,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<PagedResult<ReplyDto>> GetChildrenAsync(
        int rootReplyId, int page, ClaimsPrincipal user, CancellationToken ct)
    {
        var root = await db.Replies.SingleOrDefaultAsync(r => r.Id == rootReplyId && r.ParentReplyId == null, ct)
            ?? throw new ApiException(404, "Reply not found.");
        var thread = await db.Threads.Visible().SingleOrDefaultAsync(t => t.Id == root.ThreadId, ct)
            ?? throw new ApiException(404, "Reply not found.");
        var readableReplies = ReadableRepliesQuery(thread, user);
        if (!await readableReplies.AnyAsync(r => r.Id == rootReplyId, ct))
        {
            throw new ApiException(404, "Reply not found.");
        }

        page = Math.Max(page, 1);
        const int pageSize = ContentLimits.ChildrenPageSize;

        var query = readableReplies
            .Where(r => r.RootReplyId == rootReplyId)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);

        var totalItems = await query.CountAsync(ct);
        var children = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var allReadableChildren = await readableReplies.Where(r => r.RootReplyId == rootReplyId).ToListAsync(ct);
        var childCounts = CountDescendants(allReadableChildren);
        var mappedChildren = await ReplyMapper.MapAsync(db, children, ViewerContext.From(user), ct);
        foreach (var dto in mappedChildren)
        {
            dto.ChildCount = childCounts.GetValueOrDefault(dto.Id);
        }

        return new PagedResult<ReplyDto>
        {
            Items = mappedChildren,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    private IQueryable<Reply> ReadableRepliesQuery(ForumThread thread, ClaimsPrincipal user)
    {
        var query = db.Replies.Where(r => r.ThreadId == thread.Id);
        var role = Roles.Of(user);
        if (Roles.IsAdmin(role) || ResourceOwnership.IsThreadOwner(user, thread))
        {
            return query;
        }

        if (role == Roles.User && int.TryParse(user.FindFirst("sub")?.Value, out var userId))
        {
            return query.Where(r =>
                (r.Status == "visible" && r.HiddenAncestorCount == 0) || r.AuthorUserId == userId);
        }

        return query.Where(r => r.Status == "visible" && r.HiddenAncestorCount == 0);
    }

    private static Dictionary<int, int> CountDescendants(List<Reply> replies)
    {
        var childrenByParent = replies
            .Where(reply => reply.ParentReplyId is not null)
            .GroupBy(reply => reply.ParentReplyId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(reply => reply.Id).ToList());
        var counts = new Dictionary<int, int>(replies.Count);

        foreach (var reply in replies)
        {
            var count = 0;
            var pending = new Stack<int>(childrenByParent.GetValueOrDefault(reply.Id) ?? []);
            while (pending.TryPop(out var childId))
            {
                count++;
                if (childrenByParent.TryGetValue(childId, out var childIds))
                {
                    foreach (var descendantId in childIds) pending.Push(descendantId);
                }
            }
            counts[reply.Id] = count;
        }

        return counts;
    }

    public async Task<ReplyDto> UpdateAsync(
        int id, UpdateReplyRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var reply = await db.Replies.Visible().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Reply not found.");

        if (!ResourceOwnership.IsOwner(user, reply.AuthorUserId, reply.AuthorAdminId))
        {
            throw new ApiException(403, "You do not own this reply.");
        }

        var content = request.Content.Trim();
        if (content.Length > ContentLimits.ReplyContentMax)
        {
            throw new ApiException(400, $"Content must be at most {ContentLimits.ReplyContentMax} characters.");
        }

        var hasImage = await db.Attachments.AnyAsync(a => a.ReplyId == id, ct);
        if (!hasImage && content.Length < 1)
        {
            throw new ApiException(400, "Reply must have content or an image.");
        }

        reply.Content = content;
        reply.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return (await ReplyMapper.MapAsync(db, [reply], ViewerContext.From(user), ct)).Single();
    }

    public async Task DeleteAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var reply = await db.Replies.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Reply not found.");
        var thread = await db.Threads.SingleAsync(t => t.Id == reply.ThreadId, ct);

        if (!ResourceOwnership.CanDeleteReply(user, reply, thread))
        {
            throw new ApiException(403, "You are not allowed to delete this reply.");
        }

        var allReplies = await db.Replies.Where(r => r.ThreadId == thread.Id)
            .Select(r => new { r.Id, r.ParentReplyId })
            .ToListAsync(ct);
        var toDelete = new HashSet<int> { id };
        bool changed;
        do
        {
            changed = false;
            foreach (var r in allReplies)
            {
                if (r.ParentReplyId is int pid && toDelete.Contains(pid) && toDelete.Add(r.Id))
                {
                    changed = true;
                }
            }
        }
        while (changed);

        var attachments = await db.Attachments
            .Where(a => a.ReplyId != null && toDelete.Contains(a.ReplyId!.Value))
            .Select(a => new { a.StoredName, a.ThumbName })
            .ToListAsync(ct);

        var deletedReplies = await db.Replies
            .Where(r => toDelete.Contains(r.Id))
            .ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Replies.RemoveRange(deletedReplies);
        WriteDeleteAudit(user, reply, deletedReplies.Count);
        await db.SaveChangesAsync(ct);

        await RecalculateThreadCountersAsync(thread, ct);
        await RecalculateRootChildCountsAsync(thread.Id, ct);
        await tx.CommitAsync(ct);

        foreach (var a in attachments)
        {
            imageProcessing.DeleteFiles(a.StoredName, a.ThumbName);
        }

    }

    public async Task<ReplyDto> SetStatusAsync(int id, string status, ClaimsPrincipal user, CancellationToken ct)
    {
        if (status is not ("visible" or "hidden"))
        {
            throw new ApiException(400, "Status must be 'visible' or 'hidden'.");
        }

        var reply = await db.Replies.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ApiException(404, "Reply not found.");
        var thread = await db.Threads.SingleAsync(t => t.Id == reply.ThreadId, ct);

        var isAdmin = Roles.IsAdmin(Roles.Of(user));
        if (!isAdmin && !ResourceOwnership.IsThreadOwner(user, thread))
        {
            throw new ApiException(403, "Only the thread owner or an admin can hide/unhide replies.");
        }

        if (status == "hidden")
        {
            if (reply.Status == "hidden")
            {
                throw new ApiException(400, "Reply is already hidden.");
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var previousStatus = reply.Status;
            reply.Status = "hidden";
            reply.HiddenBy = isAdmin ? "admin" : "owner";
            WriteStatusAudit(user, isAdmin, AuditActions.Hide, id, previousStatus);
            await db.SaveChangesAsync(ct);
            await UpdateDescendantHiddenCountAsync(id, +1, ct);
            await RecalculateThreadCountersAsync(thread, ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            if (reply.Status == "visible")
            {
                throw new ApiException(400, "Reply is already visible.");
            }
            if (reply.HiddenBy == "admin" && !isAdmin)
            {
                throw new ApiException(403, "Only an admin can unhide this reply.");
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var previousStatus = reply.Status;
            reply.Status = "visible";
            reply.HiddenBy = null;
            WriteStatusAudit(user, isAdmin, AuditActions.Unhide, id, previousStatus);
            await db.SaveChangesAsync(ct);
            await UpdateDescendantHiddenCountAsync(id, -1, ct);
            await RecalculateThreadCountersAsync(thread, ct);
            await tx.CommitAsync(ct);
        }

        return (await ReplyMapper.MapAsync(db, [reply], ViewerContext.From(user), ct)).Single();
    }

    // X.2: chỉ hành động của admin mới vào audit log — chủ thread tự ẩn reply trong
    // thread của mình là nghiệp vụ thường, không phải hành động điều hành.
    private void WriteStatusAudit(
        ClaimsPrincipal user, bool isAdmin, string action, int replyId, string previousStatus)
    {
        if (!isAdmin)
        {
            return;
        }
        audit.Write(AuthorContext.Resolve(user).AdminId, action, AuditTargetTypes.Reply, replyId,
            metadata: new { status = previousStatus });
    }

    private void WriteDeleteAudit(ClaimsPrincipal user, Reply reply, int removedCount)
    {
        if (!Roles.IsAdmin(Roles.Of(user)))
        {
            return;
        }
        audit.Write(AuthorContext.Resolve(user).AdminId, AuditActions.Delete, AuditTargetTypes.Reply, reply.Id,
            metadata: new { reply.Content, reply.Status, removedCount });
    }

    private async Task RecalculateThreadCountersAsync(ForumThread thread, CancellationToken ct)
    {
        var visibleReplyIds = await db.Replies.Visible()
            .Where(r => r.ThreadId == thread.Id)
            .Select(r => new { r.Id, r.CreatedAt })
            .ToListAsync(ct);

        thread.ReplyCount = visibleReplyIds.Count;
        thread.LastReplyAt = visibleReplyIds.Count > 0
            ? visibleReplyIds.Max(r => r.CreatedAt)
            : thread.CreatedAt;

        var replyImageCount = visibleReplyIds.Count == 0
            ? 0
            : await db.Attachments.CountAsync(
                a => a.ReplyId != null && visibleReplyIds.Select(r => r.Id).Contains(a.ReplyId.Value), ct);
        var threadImageCount = await db.Attachments.CountAsync(a => a.ThreadId == thread.Id, ct);
        thread.ImageCount = threadImageCount + replyImageCount;
        await db.SaveChangesAsync(ct);
    }

    private async Task RecalculateRootChildCountsAsync(int threadId, CancellationToken ct)
    {
        var roots = await db.Replies
            .Where(r => r.ThreadId == threadId && r.ParentReplyId == null)
            .ToListAsync(ct);

        foreach (var root in roots)
        {
            root.ChildCount = await db.Replies.Visible()
                .CountAsync(r => r.ThreadId == threadId && r.RootReplyId == root.Id, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task UpdateDescendantHiddenCountAsync(int replyId, int delta, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH RECURSIVE descendants AS (
                SELECT id FROM replies WHERE parent_reply_id = {replyId}
                UNION ALL
                SELECT r.id FROM replies r
                INNER JOIN descendants d ON r.parent_reply_id = d.id
            )
            UPDATE replies
            SET hidden_ancestor_count = hidden_ancestor_count + {delta}
            WHERE id IN (SELECT id FROM descendants)
            """, ct);
    }
}