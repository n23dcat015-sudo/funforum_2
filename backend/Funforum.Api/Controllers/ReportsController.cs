using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Dtos.Reports;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

// X.3 — nút Report trên thread/reply.
[ApiController]
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken ct)
    {
        var id = await reportService.CreateAsync(request, User, ct);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }
}
