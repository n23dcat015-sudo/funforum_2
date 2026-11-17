using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Appeals;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

// X.5 — user bị ban xem trạng thái và nộp đơn kháng cáo cho lệnh ban của mình.
[ApiController]
[Route("api/appeals")]
[Authorize(Policy = AuthPolicies.UserOnly)]
public sealed class AppealsController(IAppealService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await service.GetMineAsync(GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppealRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, GetUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    private int GetUserId() => int.Parse(User.FindFirst("sub")!.Value);
}
