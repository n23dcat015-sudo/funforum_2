using System.Security.Claims;
using Funforum.Api.Dtos.Votes;

namespace Funforum.Api.Services;

public interface IVoteService
{
    Task<VoteResultDto> SetThreadVoteAsync(int threadId, short value, ClaimsPrincipal user, CancellationToken ct);

    Task<VoteResultDto> SetReplyVoteAsync(int replyId, short value, ClaimsPrincipal user, CancellationToken ct);
}