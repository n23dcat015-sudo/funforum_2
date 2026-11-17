using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Boards;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public class BoardService(FunforumDbContext db, IOptions<ContentLimitsOptions> contentLimitsOptions) : IBoardService
{
    private readonly ContentLimitsOptions _limits = contentLimitsOptions.Value;

    public async Task<IReadOnlyList<BoardDto>> ListAsync(CancellationToken ct)
    {
        var boards = await db.Boards.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(ct);
        return boards.Select(ToDto).ToList();
    }

    public async Task<BoardDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var board = await FindBoardAsync(slug, ct);
        return ToDto(board);
    }

    public async Task<PagedResult<ThreadDto>> GetThreadsAsync(
        string slug, int page, int pageSize, string? q, string sort, string? timeframe,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var board = await FindBoardAsync(slug, ct);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Threads.Visible().Where(t => t.BoardId == board.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{LikePatternEscaper.Escape(q.Trim())}%";
            var escapeChar = LikePatternEscaper.EscapeCharacter.ToString();
            query = query.Where(t =>
                EF.Functions.ILike(t.Title, pattern, escapeChar) ||
                EF.Functions.ILike(t.Content, pattern, escapeChar));
        }

        if (sort == "new")
        {
            query = query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id);
        }
        else if (sort == "top")
        {
            var cutoff = TimeframeCutoff(timeframe);
            if (cutoff is DateTime c)
            {
                query = query.Where(t => t.CreatedAt >= c);
            }
            query = query.OrderByDescending(t => t.Score).ThenByDescending(t => t.Id);
        }
        else if (sort == "hot")
        {
            query = query.OrderByDescending(t => t.HotRank).ThenByDescending(t => t.Id);
        }
        else // "latest" — mặc định (mục 4.4)
        {
            query = query.OrderByDescending(t => t.LastReplyAt).ThenByDescending(t => t.Id);
        }

        var totalItems = await query.CountAsync(ct);
        var threads = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<ThreadDto>
        {
            Items = await ThreadMapper.MapAsync(
                db, threads, board.Slug, _limits, ct, ViewerContext.From(user), includePreview: true),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    private static DateTime? TimeframeCutoff(string? timeframe)
    {
        var now = DateTime.UtcNow;
        return timeframe switch
        {
            "day" => now.AddDays(-1),
            "week" => now.AddDays(-7),
            "month" => now.AddMonths(-1),
            _ => null,
        };
    }

    private async Task<Board> FindBoardAsync(string slug, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await db.Boards.SingleOrDefaultAsync(b => b.Slug == normalized, ct)
            ?? throw new ApiException(404, "Board not found.");
    }

    private static BoardDto ToDto(Board b) => new()
    {
        Id = b.Id,
        Slug = b.Slug,
        Name = b.Name,
        Description = b.Description,
        SortOrder = b.SortOrder,
        CreatedAt = b.CreatedAt,
    };
}