using System.Security.Claims;

namespace Funforum.Api.Common;

// current_state.md mục 5: user và admin đều đăng thread/reply được; admin có thêm
// lựa chọn capcode. Dùng chung giữa ThreadService và ReplyService.
public static class AuthorContext
{
    public readonly record struct Author(int? UserId, int? AdminId, string Role);

    public static Author Resolve(ClaimsPrincipal user)
    {
        var role = Roles.Of(user)
            ?? throw new ApiException(401, "Missing role claim.");

        // 401 (không phải 403) để client tự refresh: token cấp trước X.1 mang role
        // "admin"/"superadmin" sẽ được thay bằng token mới đúng cấp.
        if (!Roles.IsValid(role))
        {
            throw new ApiException(401, "Unrecognized role claim.");
        }

        var subId = int.Parse(user.FindFirst("sub")!.Value);

        return Roles.IsAdmin(role) ? new Author(null, subId, role) : new Author(subId, null, role);
    }

    // Chỉ admin dùng được capcode; user gửi useCapcode=true bị từ chối (Phụ lục A).
    // Capcode trả về nguyên văn role, khớp CHECK ck_threads_capcode / ck_replies_capcode.
    public static string? ResolveCapcode(string role, bool useCapcode)
    {
        if (!useCapcode)
        {
            return null;
        }
        if (!Roles.IsAdmin(role))
        {
            throw new ApiException(403, "Only admins can use a capcode.");
        }
        return role;
    }
}
