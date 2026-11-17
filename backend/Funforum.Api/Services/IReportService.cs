using System.Security.Claims;
using Funforum.Api.Dtos.Reports;

namespace Funforum.Api.Services;

// X.3 — người dùng gửi report cho thread/reply.
public interface IReportService
{
    Task<long> CreateAsync(CreateReportRequest request, ClaimsPrincipal user, CancellationToken ct);
}
