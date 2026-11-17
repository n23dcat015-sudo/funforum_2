using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

public interface IAdminAdminService
{
    Task<PagedResult<AdminAccountDto>> ListAsync(int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct);
    Task<AdminAccountDto> GetAsync(int id, CancellationToken ct);
    Task<AdminAccountDto> CreateAsync(CreateAdminAccountRequest request, int actingAdminId, CancellationToken ct);
    Task<AdminAccountDto> UpdateAsync(int id, UpdateAdminAccountRequest request, int actingAdminId, CancellationToken ct);
    Task<AdminAccountDto> ChangeRoleAsync(int id, UpdateAdminRoleRequest request, int actingAdminId, CancellationToken ct);
    Task DeleteAsync(int id, int actingAdminId, CancellationToken ct);
}
