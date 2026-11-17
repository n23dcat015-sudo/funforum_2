using Funforum.Api.Common;
using Xunit;

namespace Funforum.Api.Tests.Admin;

public class AdminSecurityTests
{
    [Fact]
    public void CanDemoteAdmin_ShouldRejectDemotingLastSuperAdmin()
    {
        Assert.False(AdminSecurityGuard.CanDemoteAdmin(targetIsSuperAdmin: true, superAdminCount: 1));
    }

    [Fact]
    public void CanDemoteAdmin_ShouldAllowDemotingWhenAnotherSuperAdminExists()
    {
        Assert.True(AdminSecurityGuard.CanDemoteAdmin(targetIsSuperAdmin: true, superAdminCount: 2));
    }

    [Fact]
    public void CanDeleteAdmin_ShouldRejectSelfDelete()
    {
        var allowed = AdminSecurityGuard.CanDeleteAdmin(
            targetId: 7,
            actingAdminId: 7,
            targetRole: Roles.SuperAdmin,
            actingRole: Roles.SuperAdmin,
            superAdminCount: 1);

        Assert.False(allowed);
    }

    [Fact]
    public void CanDeleteAdmin_ShouldRejectRemovingLastSuperAdmin()
    {
        var allowed = AdminSecurityGuard.CanDeleteAdmin(
            targetId: 9,
            actingAdminId: 3,
            targetRole: Roles.SuperAdmin,
            actingRole: Roles.SuperAdmin,
            superAdminCount: 1);

        Assert.False(allowed);
    }
}
