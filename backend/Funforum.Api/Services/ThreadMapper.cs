using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

internal static class ThreadMapper
{
    public static async Task<List<ThreadDto>> MapAsync(
        FunforumDbContext db, List<ForumThread> threads, string boardSlug, ContentLimitsOptions limits,
        CancellationToken ct, ViewerContext viewer, bool includePreview = false)
    {
        var users = await AuthorLookup.GetDisplayNamesAsync(
            db, threads.Where(t => t.AuthorUserId != null).Select(t => t.AuthorUserId!.Value), ct);
        var admins = await AuthorLookup.GetAdminUsernamesAsync(
            db, threads.Where(t => t.AuthorAdminId != null).Select(t => t.AuthorAdminId!.Value), ct);

        var threadIds = threads.Select(t => t.Id).ToList();
        var attachments = await db.Attachments
            .Where(a => a.ThreadId != null && threadIds.Contains(a.ThreadId!.Value))
            .ToDictionaryAsync(a => a.ThreadId!.Value, a => a, ct);

        var totalReplyCounts = threadIds.Count == 0
            ? new Dictionary<int, int>()
            : await db.Replies.Where(r => threadIds.Contains(r.ThreadId))
                .GroupBy(r => r.ThreadId)
                .Select(g => new { ThreadId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ThreadId, x => x.Count, ct);

        var dtos = threads.Select(t => new ThreadDto
        {
            Id = t.Id,
            BoardId = t.BoardId,
            BoardSlug = boardSlug,
            Title = t.Title,
            Content = t.Content,
            AuthorName = AuthorLookup.ResolvePublicName(t.AuthorUserId, t.AuthorAdminId, t.Capcode, users, admins),
            Capcode = t.Capcode,
            IsMine = viewer.IsAuthor(t.AuthorUserId, t.AuthorAdminId),
            CanDelete = viewer.IsAdmin || viewer.IsAuthor(t.AuthorUserId, t.AuthorAdminId),
            ReplyCount = t.ReplyCount,
            Score = t.Score,
            Image = attachments.TryGetValue(t.Id, out var att) ? AttachmentMapper.ToDto(att) : null,
            ImagesPosted = t.ImagesPosted,
            ImageLimitReached = t.ImagesPosted >= limits.MaxImagesPerThread,
            ReplyLimitReached = totalReplyCounts.GetValueOrDefault(t.Id) >= limits.MaxRepliesPerThread,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            LastReplyAt = t.LastReplyAt,
        }).ToList();

        if (includePreview && threadIds.Count > 0)
        {
            await AttachPreviewAsync(db, threads, dtos, threadIds, viewer, ct);
        }

        return dtos;
    }

    private static async Task AttachPreviewAsync(
        FunforumDbContext db, List<ForumThread> threads, List<ThreadDto> dtos,
        List<int> threadIds, ViewerContext viewer, CancellationToken ct)
    {
        var allVisibleReplies = await db.Replies.Visible()
            .Where(r => threadIds.Contains(r.ThreadId))
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var previewByThread = allVisibleReplies
            .GroupBy(r => r.ThreadId)
            .ToDictionary(g => g.Key, g => g.Take(3).ToList());

        var allPreviews = previewByThread.Values.SelectMany(v => v).ToList();
        var mappedPreviews = (await ReplyMapper.MapAsync(db, allPreviews, viewer, ct)).ToDictionary(p => p.Id);

        var previewReplyIds = allPreviews.Select(r => r.Id).ToList();
        var imagesInPreview = (await db.Attachments
            .Where(a => a.ReplyId != null && previewReplyIds.Contains(a.ReplyId!.Value))
            .Select(a => a.ReplyId!.Value)
            .ToListAsync(ct)).ToHashSet();

        foreach (var t in threads)
        {
            var dto = dtos.Single(d => d.Id == t.Id);

            if (!previewByThread.TryGetValue(t.Id, out var previewEntities))
            {
                dto.PreviewReplies = [];
                continue;
            }

            var ordered = previewEntities.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
            dto.PreviewReplies = ordered.Select(r => mappedPreviews[r.Id]).ToList();

            var previewImageCount = ordered.Count(r => imagesInPreview.Contains(r.Id));
            dto.OmittedReplies = Math.Max(0, t.ReplyCount - ordered.Count);
            dto.OmittedImages = Math.Max(0, t.ImageCount - previewImageCount);
        }
    }
}