namespace Funforum.Api.Common;

public static class AdminSecurityGuard
{
    public static bool CanDemoteAdmin(bool targetIsSuperAdmin, int superAdminCount) =>
        !targetIsSuperAdmin || superAdminCount > 1;

    public static bool CanDeleteAdmin(
        int targetId,
        int actingAdminId,
        string targetRole,
        string actingRole,
        int superAdminCount)
    {
        if (targetId == actingAdminId)
        {
            return false;
        }

        if (targetRole == Roles.SuperAdmin && !Roles.AtLeast(actingRole, Roles.SuperAdmin))
        {
            return false;
        }

        // Không xóa super admin cuối cùng, kể cả khi người gọi cũng là super admin.
        if (targetRole == Roles.SuperAdmin && superAdminCount <= 1)
        {
            return false;
        }

        return Roles.AtLeast(actingRole, targetRole);
    }

    // X.1: chặn hạ quyền super admin cuối — trước đây UpdateAsync gán is_super_admin
    // thẳng mà không qua guard nào, nên super admin duy nhất tự khóa được cả panel.
    public static bool CanChangeRole(
        string targetRole,
        string actingRole,
        string newRole,
        int superAdminCount)
    {
        if (!Roles.AtLeast(actingRole, Roles.SuperAdmin))
        {
            return false;
        }

        // "user" thuộc bảng users, không phải một cấp admin.
        if (!Roles.IsAdmin(newRole))
        {
            return false;
        }

        return !(targetRole == Roles.SuperAdmin && newRole != Roles.SuperAdmin && superAdminCount <= 1);
    }
}
