using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
// Ma trận quyền X.1: janitor KHÔNG được xem email/IP và lịch sử vi phạm của user.
[Authorize(Policy = AuthPolicies.AtLeastModerator)]
public sealed class AdminUsersController(IAdminUserService service) : ControllerBase
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

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdminUserRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, GetAdminId(), ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetAdminId(), ct);
        return NoContent();
    }

    // X.5 — khóa quyền kháng cáo: users.status = banned_permanently.
    [HttpPost("{id:int}/lock-appeal")]
    public async Task<IActionResult> LockAppeal(int id, CancellationToken ct) =>
        Ok(await service.LockAppealAsync(id, GetAdminId(), ct));

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
