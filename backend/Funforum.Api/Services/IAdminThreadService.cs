using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Threads;

namespace Funforum.Api.Services;

public interface IAdminThreadService
{
    Task<PagedResult<AdminThreadDto>> ListAsync(int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct);
    Task<AdminThreadDto> GetAsync(int id, CancellationToken ct);
    Task<ThreadDto> CreateAsync(CreateAdminThreadRequest request, System.Security.Claims.ClaimsPrincipal admin, CancellationToken ct);
    Task<ThreadDto> UpdateAsync(int id, UpdateThreadRequest request, System.Security.Claims.ClaimsPrincipal admin, CancellationToken ct);
    Task DeleteAsync(int id, System.Security.Claims.ClaimsPrincipal admin, CancellationToken ct);
    Task<AdminThreadDto> SetStatusAsync(int id, string status, int adminId, CancellationToken ct);
}
