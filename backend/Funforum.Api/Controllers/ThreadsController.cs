using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Replies;
using Funforum.Api.Dtos.Threads;
using Funforum.Api.Dtos.Votes;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/threads")]
public class ThreadsController(
    IThreadService threadService, IReplyService replyService, IVoteService voteService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await threadService.GetByIdAsync(id, User, ct));

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateThreadRequest request, CancellationToken ct) =>
        Ok(await threadService.UpdateAsync(id, request, User, ct));

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await threadService.DeleteAsync(id, User, ct);
        return NoContent();
    }

    [Authorize]
    [RequiresActiveUser]
    [HttpPut("{id:int}/vote")]
    public async Task<IActionResult> Vote(int id, [FromBody] VoteRequest request, CancellationToken ct) =>
        Ok(await voteService.SetThreadVoteAsync(id, request.Value, User, ct));

    [HttpGet("{id:int}/replies")]
    public async Task<IActionResult> GetReplies(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ContentLimits.RootRepliesPageSize,
        CancellationToken ct = default) =>
        Ok(await replyService.GetRootRepliesAsync(id, page, pageSize, User, ct));

    [Authorize]
    [RequiresActiveUser]
    [HttpPost("{id:int}/replies")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddReply(
        int id, [FromForm] CreateReplyRequest request, CancellationToken ct)
    {
        var dto = await replyService.AddAsync(
            id, request, User, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }
}