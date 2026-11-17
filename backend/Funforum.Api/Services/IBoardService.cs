using System.Security.Claims;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Boards;
using Funforum.Api.Dtos.Threads;

namespace Funforum.Api.Services;

public interface IBoardService
{
    Task<IReadOnlyList<BoardDto>> ListAsync(CancellationToken ct);

    Task<BoardDto> GetBySlugAsync(string slug, CancellationToken ct);

    Task<PagedResult<ThreadDto>> GetThreadsAsync(
        string slug, int page, int pageSize, string? q, string sort, string? timeframe,
        ClaimsPrincipal user, CancellationToken ct);
}