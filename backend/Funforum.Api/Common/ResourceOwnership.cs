using System.Security.Claims;
using Funforum.Api.Entities;

namespace Funforum.Api.Common;

// current_state.md mục 3.3 / mục 11.4: helper kiểm tra quyền sở hữu dùng chung cho
// thread và reply; mọi endpoint PUT/DELETE gọi qua đây, không lặp logic từng nơi.
public static class ResourceOwnership
{
    public static bool IsOwner(ClaimsPrincipal user, int? authorUserId, int? authorAdminId)
    {
        var role = Roles.Of(user);
        var subClaim = user.FindFirst("sub")?.Value;
        if (subClaim is null || !int.TryParse(subClaim, out var subId))
        {
            return false;
        }

        // users.id và admins.id là 2 dãy độc lập nên phải rẽ theo role, không so cả hai.
        if (Roles.IsAdmin(role))
        {
            return authorAdminId == subId;
        }

        return role == Roles.User && authorUserId == subId;
    }

    public static bool IsThreadOwner(ClaimsPrincipal user, ForumThread thread) =>
        IsOwner(user, thread.AuthorUserId, thread.AuthorAdminId);

    // Xóa reply: tác giả reply, chủ thread hoặc admin (mục 3.3).
    public static bool CanDeleteReply(ClaimsPrincipal user, Reply reply, ForumThread thread)
    {
        if (Roles.IsAdmin(Roles.Of(user)))
        {
            return true;
        }

        return IsOwner(user, reply.AuthorUserId, reply.AuthorAdminId) || IsThreadOwner(user, thread);
    }
}
