using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/admin/admins")]
[Authorize(Policy = AuthPolicies.AtLeastSuperAdmin)]
public sealed class AdminAdminsController(IAdminAdminService service) : ControllerBase
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
    public async Task<IActionResult> Create([FromBody] CreateAdminAccountRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, GetAdminId(), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdminAccountRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, GetAdminId(), ct));

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] UpdateAdminRoleRequest request, CancellationToken ct) =>
        Ok(await service.ChangeRoleAsync(id, request, GetAdminId(), ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetAdminId(), ct);
        return NoContent();
    }

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
