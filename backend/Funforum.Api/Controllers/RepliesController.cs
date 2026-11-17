using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Replies;
using Funforum.Api.Dtos.Votes;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/replies")]
public class RepliesController(IReplyService replyService, IVoteService voteService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await replyService.GetByIdAsync(id, User, ct));

    // mục 7.4: công khai, không cần đăng nhập.
    [AllowAnonymous]
    [HttpGet("{id:int}/children")]
    public async Task<IActionResult> GetChildren(int id, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Ok(await replyService.GetChildrenAsync(id, page, User, ct));

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateReplyRequest request, CancellationToken ct) =>
        Ok(await replyService.UpdateAsync(id, request, User, ct));

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await replyService.DeleteAsync(id, User, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(
        int id, [FromBody] UpdateReplyStatusRequest request, CancellationToken ct) =>
        Ok(await replyService.SetStatusAsync(id, request.Status, User, ct));

    [Authorize]
    [RequiresActiveUser]
    [HttpPut("{id:int}/vote")]
    public async Task<IActionResult> Vote(int id, [FromBody] VoteRequest request, CancellationToken ct) =>
        Ok(await voteService.SetReplyVoteAsync(id, request.Value, User, ct));
}