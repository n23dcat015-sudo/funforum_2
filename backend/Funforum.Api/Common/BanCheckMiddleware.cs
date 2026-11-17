using Funforum.Api.Services;

namespace Funforum.Api.Common;

// X.4 — chặn đăng bài/đăng reply/vote khi user bị ban (theo user_id hoặc IP trùng
// banned_ip active). KHÔNG revoke JWT, KHÔNG logout: token vẫn hợp lệ, chỉ 403 ở đây
// kèm hướng dẫn kháng cáo (phần UI kháng cáo thuộc X.5).
public sealed class BanCheckMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requiresActiveUser =
            context.GetEndpoint()?.Metadata.GetMetadata<RequiresActiveUserAttribute>() is not null;

        // Chỉ áp cho user thường — admin/moderator không bị ban theo cơ chế này.
        if (requiresActiveUser && Roles.Of(context.User) == Roles.User)
        {
            var sub = context.User.FindFirst("sub")?.Value;
            if (int.TryParse(sub, out var userId))
            {
                var ip = context.Connection.RemoteIpAddress?.ToString();
                var banService = context.RequestServices.GetRequiredService<IBanService>();
                if (await banService.IsBlockedAsync(userId, ip, context.RequestAborted))
                {
                    throw new ApiException(
                        403,
                        "Your account is banned. You cannot post, reply or vote. " +
                        "You can submit an appeal from the appeal page.");
                }
            }
        }

        await next(context);
    }
}
