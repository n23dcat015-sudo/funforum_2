using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

public interface IAdminAuditLogService
{
    Task<PagedResult<AuditLogDto>> ListAsync(
        int page, int pageSize, int? actorId, string? action,
        DateTime? from, DateTime? to, string? sort, string? order, CancellationToken ct);
}
