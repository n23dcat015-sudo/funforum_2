using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

// X.3 — hàng chờ report cho Janitor+.
public interface IAdminReportService
{
    Task<PagedResult<AdminReportDto>> ListAsync(
        int page, int pageSize, string? status, string? targetType,
        string? sort, string? order, CancellationToken ct);

    Task<AdminReportDto> EscalateAsync(long id, CancellationToken ct);

    Task<AdminReportDto> ResolveAsync(long id, CancellationToken ct);
}
