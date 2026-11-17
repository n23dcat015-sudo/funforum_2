using Microsoft.AspNetCore.Authorization;
using Funforum.Api.Security;

namespace Funforum.Api.Common;

public static class AuthPolicies
{
    public const string UserOnly = "UserOnly";
    public const string AtLeastJanitor = "AtLeastJanitor";
    public const string AtLeastModerator = "AtLeastModerator";
    public const string AtLeastSuperAdmin = "AtLeastSuperAdmin";
}

// Tách khỏi Program.cs để app và test dùng chung một nguồn khai báo policy.
public static class AuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        // UserOnly phải khớp CHÍNH XÁC "user", không dùng AtLeast(User): /users/me parse
        // sub trỏ vào bảng users, mà users.id và admins.id là 2 dãy độc lập trùng nhau được.
        options.AddPolicy(AuthPolicies.UserOnly,
            policy => policy.RequireClaim(Roles.ClaimType, Roles.User));

        options.AddPolicy(AuthPolicies.AtLeastJanitor,
            policy => policy.AddRequirements(new RoleRequirement(Roles.Janitor)));

        options.AddPolicy(AuthPolicies.AtLeastModerator,
            policy => policy.AddRequirements(new RoleRequirement(Roles.Moderator)));

        options.AddPolicy(AuthPolicies.AtLeastSuperAdmin,
            policy => policy.AddRequirements(new RoleRequirement(Roles.SuperAdmin)));
    }
}
