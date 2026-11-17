using Microsoft.AspNetCore.Authorization;
using Funforum.Api.Common;

namespace Funforum.Api.Security;

public sealed class RoleRequirementHandler : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        if (Roles.AtLeast(Roles.Of(context.User), requirement.MinimumRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
