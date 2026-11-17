using System.Security.Claims;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Replies;

namespace Funforum.Api.Services;

public interface IReplyService
{
    Task<ReplyDto> AddAsync(
        int threadId, CreateReplyRequest request, ClaimsPrincipal user, string? ip, CancellationToken ct);

    Task<ReplyDto> GetByIdAsync(int id, ClaimsPrincipal user, CancellationToken ct);

    Task<PagedResult<ReplyDto>> GetRootRepliesAsync(
        int threadId, int page, int pageSize, ClaimsPrincipal user, CancellationToken ct);

    // mục 7.4: reply cấp 2 của một nhánh, phân trang 10/lần, cũ đến mới.
    Task<PagedResult<ReplyDto>> GetChildrenAsync(int rootReplyId, int page, ClaimsPrincipal user, CancellationToken ct);

    Task<ReplyDto> UpdateAsync(int id, UpdateReplyRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task DeleteAsync(int id, ClaimsPrincipal user, CancellationToken ct);

    // mục 3.3: ẩn/hiện reply, lan xuống toàn nhánh dưới bằng recursive CTE.
    Task<ReplyDto> SetStatusAsync(int id, string status, ClaimsPrincipal user, CancellationToken ct);
}