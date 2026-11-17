using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Boards;
using Funforum.Api.Entities;
using Funforum.Api.Security;

namespace Funforum.Api.Services;

public sealed partial class AdminBoardService(
    FunforumDbContext db,
    IAuditLogger audit,
    ILogger<AdminBoardService> logger) : IAdminBoardService
{
    public async Task<PagedResult<BoardDto>> ListAsync(
        int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Boards.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(b => b.Slug.Contains(term) || b.Name.Contains(term));
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(b => b.Id) : query.OrderBy(b => b.Id),
            "slug" => descending ? query.OrderByDescending(b => b.Slug).ThenByDescending(b => b.Id) : query.OrderBy(b => b.Slug).ThenBy(b => b.Id),
            "name" => descending ? query.OrderByDescending(b => b.Name).ThenByDescending(b => b.Id) : query.OrderBy(b => b.Name).ThenBy(b => b.Id),
            "createdat" => descending ? query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id) : query.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id),
            _ => descending ? query.OrderByDescending(b => b.SortOrder).ThenByDescending(b => b.Id) : query.OrderBy(b => b.SortOrder).ThenBy(b => b.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var boards = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<BoardDto>
        {
            Items = boards.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<BoardDto> GetAsync(int id, CancellationToken ct) =>
        ToDto(await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new ApiException(404, "Board not found."));

    public async Task<BoardDto> CreateAsync(CreateAdminBoardRequest request, int adminId, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var name = request.Name.Trim();
        ValidateSlug(slug);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApiException(400, "Board name is required.");
        }

        if (await db.Boards.AnyAsync(b => b.Slug == slug, ct))
        {
            throw new ApiException(409, "Board slug already exists.");
        }

        var board = new Board
        {
            Slug = slug,
            Name = name,
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        // board.Id chỉ có sau INSERT nên không thể stage log nguyên tử cùng lượt save.
        audit.Write(adminId, AuditActions.BoardCreate, AuditTargetTypes.Board, board.Id);
        await audit.FlushAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "create", "board", board.Id);
        return ToDto(board);
    }

    public async Task<BoardDto> UpdateAsync(int id, UpdateAdminBoardRequest request, int adminId, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new ApiException(404, "Board not found.");
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApiException(400, "Board name is required.");
        }

        board.Name = name;
        board.Description = request.Description?.Trim();
        board.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "update", "board", id);
        return ToDto(board);
    }

    public async Task DeleteAsync(int id, int adminId, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new ApiException(404, "Board not found.");
        if (await db.Threads.AnyAsync(t => t.BoardId == id, ct))
        {
            throw new ApiException(409, "Cannot delete a board that still contains threads.");
        }

        // Metadata = toàn bộ trạng thái trước khi xoá, đủ để khôi phục board.
        audit.Write(adminId, AuditActions.BoardDelete, AuditTargetTypes.Board, id, metadata: new
        {
            board.Slug,
            board.Name,
            board.Description,
            board.SortOrder,
            board.CreatedAt,
        });
        db.Boards.Remove(board);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "delete", "board", id);
    }

    private static void ValidateSlug(string slug)
    {
        if (!SlugRegex().IsMatch(slug))
        {
            throw new ApiException(400, "Slug must contain only lowercase letters, numbers, and single hyphens.");
        }
        if (ReservedUsernames.IsReserved(slug))
        {
            throw new ApiException(400, "This board slug is reserved.");
        }
    }

    private static BoardDto ToDto(Board board) => new()
    {
        Id = board.Id,
        Slug = board.Slug,
        Name = board.Name,
        Description = board.Description,
        SortOrder = board.SortOrder,
        CreatedAt = board.CreatedAt,
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
