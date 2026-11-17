using System.Security.Claims;
using Funforum.Api.Common;
using Xunit;

namespace Funforum.Api.Tests.Common;

// Phụ lục A: user gửi useCapcode=true phải bị từ chối 403; chỉ janitor/moderator/
// super_admin dùng được capcode (current_state.md mục 5, X.1).
public class AuthorContextTests
{
    private static ClaimsPrincipal Principal(string role, int sub) =>
        new(new ClaimsIdentity([new Claim("sub", sub.ToString()), new Claim("role", role)]));

    [Fact]
    public void Resolve_UserRole_SetsUserIdOnly()
    {
        var author = AuthorContext.Resolve(Principal(Roles.User, 42));

        Assert.Equal(42, author.UserId);
        Assert.Null(author.AdminId);
        Assert.Equal(Roles.User, author.Role);
    }

    [Fact]
    public void Resolve_StaffRole_SetsAdminIdOnly()
    {
        var author = AuthorContext.Resolve(Principal(Roles.Moderator, 7));

        Assert.Null(author.UserId);
        Assert.Equal(7, author.AdminId);
        Assert.Equal(Roles.Moderator, author.Role);
    }

    [Fact]
    public void ResolveCapcode_UserRequestsCapcode_Throws403()
    {
        var ex = Assert.Throws<ApiException>(() => AuthorContext.ResolveCapcode(Roles.User, useCapcode: true));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public void ResolveCapcode_ModeratorRequestsCapcode_ReturnsRole()
    {
        Assert.Equal(Roles.Moderator, AuthorContext.ResolveCapcode(Roles.Moderator, useCapcode: true));
    }

    [Fact]
    public void ResolveCapcode_SuperAdminRequestsCapcode_ReturnsRole()
    {
        Assert.Equal(Roles.SuperAdmin, AuthorContext.ResolveCapcode(Roles.SuperAdmin, useCapcode: true));
    }

    [Fact]
    public void ResolveCapcode_NotRequested_ReturnsNull()
    {
        Assert.Null(AuthorContext.ResolveCapcode(Roles.User, useCapcode: false));
        Assert.Null(AuthorContext.ResolveCapcode(Roles.Moderator, useCapcode: false));
    }
}
