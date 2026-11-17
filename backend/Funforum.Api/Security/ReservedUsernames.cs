namespace Funforum.Api.Security;

// current_state.md mục 5: chặn đăng ký username dễ giả mạo vì capcode là dấu
// hiệu xác thực duy nhất của admin.
public static class ReservedUsernames
{
    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "administrator",
        "janitor",
        "mod",
        "moderator",
        "funforum",
        "super_admin",
        "system",
    };

    public static bool IsReserved(string username) => Values.Contains(username);
}
