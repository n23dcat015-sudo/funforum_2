using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

// X.4 — Moderator+ quản lý ban tài khoản/IP.
[ApiController]
[Route("api/admin/bans")]
[Authorize(Policy = AuthPolicies.AtLeastModerator)]
public sealed class AdminBansController(IBanService service) : ControllerBase
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBanRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, GetAdminId(), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPost("{id:long}/unban")]
    public async Task<IActionResult> Unban(long id, CancellationToken ct) =>
        Ok(await service.UnbanAsync(id, GetAdminId(), ct));

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
