using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

// X.5 — Moderator+ duyệt đơn kháng cáo (approve mở ban, reject khóa nộp lại).
[ApiController]
[Route("api/admin/appeals")]
[Authorize(Policy = AuthPolicies.AtLeastModerator)]
public sealed class AdminAppealsController(IAdminAppealService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        CancellationToken ct = default) =>
        Ok(await service.ListAsync(page, pageSize, status, userId, sort, order, ct));

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Review(long id, [FromBody] ReviewAppealRequest request, CancellationToken ct) =>
        Ok(await service.ReviewAsync(id, request.Decision, GetAdminId(), ct));

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
