using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/admin/boards")]
// Ma trận quyền X.1: moderator+ chỉ ĐỌC board; tạo/sửa/xóa dành riêng cho super admin.
[Authorize(Policy = AuthPolicies.AtLeastModerator)]
public sealed class AdminBoardsController(IAdminBoardService service) : ControllerBase
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
    [Authorize(Policy = AuthPolicies.AtLeastSuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateAdminBoardRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, GetAdminId(), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.AtLeastSuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdminBoardRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, GetAdminId(), ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.AtLeastSuperAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetAdminId(), ct);
        return NoContent();
    }

    private int GetAdminId() => int.Parse(User.FindFirst("sub")!.Value);
}
