using System.Security.Claims;

namespace Funforum.Api.Common;

// X.1: admin có 3 cấp janitor < moderator < super_admin; "user" là tài khoản thường.
// Mọi nơi cần xét quyền phải đi qua helper này — token cũ mang role "admin"/"superadmin"
// cho Tier = -1 nên bị từ chối, không âm thầm rơi vào nhánh admin như trước.
public static class Roles
{
    public const string ClaimType = "role";

    public const string User = "user";
    public const string Janitor = "janitor";
    public const string Moderator = "moderator";
    public const string SuperAdmin = "super_admin";

    public static readonly string[] AdminRoles = [Janitor, Moderator, SuperAdmin];

    public static int Tier(string? role) => role switch
    {
        User => 0,
        Janitor => 1,
        Moderator => 2,
        SuperAdmin => 3,
        _ => -1,
    };

    public static bool IsValid(string? role) => Tier(role) >= 0;

    public static bool IsAdmin(string? role) => Tier(role) >= Tier(Janitor);

    public static bool AtLeast(string? role, string minimum) => Tier(role) >= Tier(minimum);

    public static string? Of(ClaimsPrincipal user) => user.FindFirst(ClaimType)?.Value;
}
