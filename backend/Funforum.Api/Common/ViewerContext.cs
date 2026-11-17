using System.Security.Claims;

namespace Funforum.Api.Common;

// Phase A: ngữ cảnh người gọi để tính cờ sở hữu (isMine/canDelete) trên DTO công khai.
// Trước đây FE tự so sánh chuỗi authorName === username — sau khi mọi user hiển thị
// "Anonymous" thì cách đó vỡ, nên backend trả cờ tính sẵn (auth_google_email_plan.md mục 1.4).
public readonly record struct ViewerContext(int? UserId, int? AdminId, bool IsAdmin)
{
    public static readonly ViewerContext Anonymous = new(null, null, false);

    public static ViewerContext From(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Anonymous;
        }

        var role = Roles.Of(user);
        if (role is null || !Roles.IsValid(role) || !int.TryParse(user.FindFirst("sub")?.Value, out var subId))
        {
            return Anonymous;
        }

        // users.id và admins.id là 2 dãy độc lập nên phải rẽ theo role, không so cả hai.
        return Roles.IsAdmin(role)
            ? new ViewerContext(null, subId, true)
            : new ViewerContext(subId, null, false);
    }

    public bool IsAuthor(int? authorUserId, int? authorAdminId) =>
        (UserId is int userId && authorUserId == userId) ||
        (AdminId is int adminId && authorAdminId == adminId);
}
