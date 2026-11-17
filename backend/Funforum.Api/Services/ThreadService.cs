using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public class ThreadService(
    FunforumDbContext db, IImageProcessingService imageProcessing, IOptions<ContentLimitsOptions> contentLimitsOptions)
    : IThreadService
{
    private readonly ContentLimitsOptions _limits = contentLimitsOptions.Value;

    public async Task<ThreadDto> CreateAsync(
        string boardSlug, CreateThreadRequest request, ClaimsPrincipal user, string? ip, CancellationToken ct)
    {
        var normalizedSlug = boardSlug.Trim().ToLowerInvariant();
        var board = await db.Boards.SingleOrDefaultAsync(b => b.Slug == normalizedSlug, ct)
            ?? throw new ApiException(404, "Board not found.");

        var title = request.Title.Trim();
        var content = request.Content.Trim();
        ValidateTitleAndContent(title, content);

        var author = AuthorContext.Resolve(user);
        var capcode = AuthorContext.ResolveCapcode(author.Role, request.UseCapcode);

        ProcessedImage? processed = null;
        if (request.Image is { Length: > 0 })
        {
            processed = await imageProcessing.ProcessAndSaveAsync(request.Image, ct);
        }

        var now = DateTime.UtcNow;
        var thread = new ForumThread
        {
            BoardId = board.Id,
            AuthorUserId = author.UserId,
            AuthorAdminId = author.AdminId,
            Capcode = capcode,
            Title = title,
            Content = content,
            Status = "visible",
            PosterIp = ip, // X.4
            LastReplyAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Threads.Add(thread);
            await db.SaveChangesAsync(ct);

            if (processed is not null)
            {
                db.Attachments.Add(new Attachment
                {
                    ThreadId = thread.Id,
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

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            if (processed is not null)
            {
                imageProcessing.DeleteFiles(processed.StoredName, processed.ThumbName);
            }
            throw;
        }

        return (await ThreadMapper.MapAsync(db, [thread], board.Slug, _limits, ct, ViewerContext.From(user))).Single();
    }

    public async Task<ThreadDto> GetByIdAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var thread = await db.Threads.Visible().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ApiException(404, "Thread not found.");
        var slug = await db.Boards.Where(b => b.Id == thread.BoardId).Select(b => b.Slug).SingleAsync(ct);

        return (await ThreadMapper.MapAsync(db, [thread], slug, _limits, ct, ViewerContext.From(user))).Single();
    }

    public async Task<ThreadDto> UpdateAsync(
        int id, UpdateThreadRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var thread = await db.Threads.Visible().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ApiException(404, "Thread not found.");

        var isAdmin = Roles.IsAdmin(Roles.Of(user));
        if (!isAdmin && !ResourceOwnership.IsThreadOwner(user, thread))
        {
            throw new ApiException(403, "You do not own this thread.");
        }

        var title = request.Title.Trim();
        var content = request.Content.Trim();
        ValidateTitleAndContent(title, content);

        thread.Title = title;
        thread.Content = content;
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var slug = await db.Boards.Where(b => b.Id == thread.BoardId).Select(b => b.Slug).SingleAsync(ct);
        return (await ThreadMapper.MapAsync(db, [thread], slug, _limits, ct, ViewerContext.From(user))).Single();
    }

    public async Task DeleteAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ApiException(404, "Thread not found.");

        var isAdmin = Roles.IsAdmin(Roles.Of(user));
        if (!isAdmin && !ResourceOwnership.IsThreadOwner(user, thread))
        {
            throw new ApiException(403, "You do not own this thread.");
        }

        var attachments = await db.Attachments
            .Where(a => a.ThreadId == id || (a.ReplyId != null &&
                db.Replies.Any(r => r.Id == a.ReplyId && r.ThreadId == id)))
            .Select(a => new { a.StoredName, a.ThumbName })
            .ToListAsync(ct);

        db.Threads.Remove(thread);
        await db.SaveChangesAsync(ct);

        foreach (var a in attachments)
        {
            imageProcessing.DeleteFiles(a.StoredName, a.ThumbName);
        }
    }

    private static void ValidateTitleAndContent(string title, string content)
    {
        if (title.Length is < 1 or > ContentLimits.ThreadTitleMax)
        {
            throw new ApiException(400, $"Title must be 1-{ContentLimits.ThreadTitleMax} characters.");
        }
        if (content.Length is < 1 or > ContentLimits.ThreadContentMax)
        {
            throw new ApiException(400, $"Content must be 1-{ContentLimits.ThreadContentMax} characters.");
        }
    }
}