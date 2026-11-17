using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

// X.3 — hàng chờ report cho Janitor+ (Moderator lọc ?status=escalated).
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = AuthPolicies.AtLeastJanitor)]
public sealed class AdminReportsController(IAdminReportService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? targetType = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        CancellationToken ct = default) =>
        Ok(await service.ListAsync(page, pageSize, status, targetType, sort, order, ct));

    [HttpPatch("{id:long}/escalate")]
    public async Task<IActionResult> Escalate(long id, CancellationToken ct) =>
        Ok(await service.EscalateAsync(id, ct));

    [HttpPatch("{id:long}/resolve")]
    public async Task<IActionResult> Resolve(long id, CancellationToken ct) =>
        Ok(await service.ResolveAsync(id, ct));
}
