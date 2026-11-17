using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Boards;

namespace Funforum.Api.Services;

public interface IAdminBoardService
{
    Task<PagedResult<BoardDto>> ListAsync(int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct);
    Task<BoardDto> GetAsync(int id, CancellationToken ct);
    Task<BoardDto> CreateAsync(CreateAdminBoardRequest request, int adminId, CancellationToken ct);
    Task<BoardDto> UpdateAsync(int id, UpdateAdminBoardRequest request, int adminId, CancellationToken ct);
    Task DeleteAsync(int id, int adminId, CancellationToken ct);
}
