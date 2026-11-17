using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/admin/threads")]
[Authorize(Policy = AuthPolicies.AtLeastJanitor)]
public sealed class AdminThreadsController(IAdminThreadService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        CancellationToken ct = default) =>
        Ok(await service.ListAsync(page, pageSize, q, sort, order, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateAdminThreadRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, User, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateThreadRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, User, ct));

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] UpdateAdminThreadStatusRequest request, CancellationToken ct) =>
        Ok(await service.SetStatusAsync(id, request.Status.Trim().ToLowerInvariant(), GetAdminId(), ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, User, ct);
        return NoContent();
    }

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
