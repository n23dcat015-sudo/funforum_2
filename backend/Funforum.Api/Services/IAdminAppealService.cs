using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

// X.5 — hàng chờ kháng cáo cho Moderator+.
public interface IAdminAppealService
{
    Task<PagedResult<AppealDto>> ListAsync(
        int page, int pageSize, string? status, int? userId,
        string? sort, string? order, CancellationToken ct);

    Task<AppealDto> ReviewAsync(long id, string? decision, int actingAdminId, CancellationToken ct);
}
