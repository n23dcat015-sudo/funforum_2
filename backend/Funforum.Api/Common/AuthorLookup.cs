using Microsoft.EntityFrameworkCore;
using Funforum.Api.Data;

namespace Funforum.Api.Common;

// Tra tên hiển thị theo lô. DTO công khai chỉ có authorName + capcode, không bao giờ
// trả author_user_id/author_admin_id (mục 11.5).
public static class AuthorLookup
{
    // Phase A: user hiển thị display_name (mặc định "Anonymous"), KHÔNG phải username
    // (username tự sinh, chỉ dùng nội bộ).
    public static async Task<Dictionary<int, string>> GetDisplayNamesAsync(
        FunforumDbContext db, IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<int, string>();
        }
        return await db.Users.Where(u => idList.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public static async Task<Dictionary<int, string>> GetAdminUsernamesAsync(
        FunforumDbContext db, IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<int, string>();
        }
        return await db.Admins.Where(a => idList.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Username, ct);
    }

    public static string ResolveName(
        int? userId, int? adminId,
        IReadOnlyDictionary<int, string> users, IReadOnlyDictionary<int, string> admins)
    {
        if (userId is int uid && users.TryGetValue(uid, out var uname))
        {
            return uname;
        }
        if (adminId is int aid && admins.TryGetValue(aid, out var aname))
        {
            return aname;
        }
        return "[deleted]";
    }

    public static string ResolvePublicName(
        int? userId, int? adminId, string? capcode,
        IReadOnlyDictionary<int, string> users, IReadOnlyDictionary<int, string> admins)
    {
        if (adminId is not null && capcode is null)
        {
            return "Anonymous";
        }

        return ResolveName(userId, adminId, users, admins);
    }
}