using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/boards")]
public class BoardsController(IBoardService boardService, IThreadService threadService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await boardService.ListAsync(ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct) =>
        Ok(await boardService.GetBySlugAsync(slug, ct));

    // current_state.md mục 4.4: 10 thread/trang; GĐ3b thêm sort Top + `t` (khung thời gian).
    // Latest (mặc định thật) và Hot làm ở GĐ3f/3c.
    [HttpGet("{slug}/threads")]
    public async Task<IActionResult> GetThreads(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ContentLimits.BoardIndexPageSize,
        [FromQuery] string? q = null,
        [FromQuery] string sort = "latest",
        [FromQuery(Name = "t")] string? timeframe = null,
        CancellationToken ct = default) =>
        Ok(await boardService.GetThreadsAsync(slug, page, pageSize, q, sort, timeframe, User, ct));

    [Authorize]
    [RequiresActiveUser]
    [HttpPost("{slug}/threads")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateThread(
        string slug, [FromForm] CreateThreadRequest request, CancellationToken ct)
    {
        var dto = await threadService.CreateAsync(
            slug, request, User, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }
}