using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserDto>> ListAsync(int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct);
    Task<AdminUserDto> GetAsync(int id, CancellationToken ct);
    Task<AdminUserDto> UpdateAsync(int id, UpdateAdminUserRequest request, int adminId, CancellationToken ct);
    Task<AdminUserDto> LockAppealAsync(int id, int adminId, CancellationToken ct);
    Task DeleteAsync(int id, int adminId, CancellationToken ct);
}
