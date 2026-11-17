namespace Funforum.Api.Common;

// X.4 — trạng thái tài khoản người dùng (khớp CHECK users.status trong DB).
public static class UserStatuses
{
    public const string Active = "active";
    public const string Banned = "banned";
    public const string BannedPermanently = "banned_permanently";

    public static readonly string[] All = [Active, Banned, BannedPermanently];

    public static bool IsBanned(string status) => status is Banned or BannedPermanently;
}

// X.4 — trạng thái của một bản ghi ban (khớp CHECK bans.status trong DB).
public static class BanStatuses
{
    public const string Active = "active";
    public const string Lifted = "lifted";

    public static readonly string[] All = [Active, Lifted];

    public static bool IsValid(string status) => All.Contains(status);
}
