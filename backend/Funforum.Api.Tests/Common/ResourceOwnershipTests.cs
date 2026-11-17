using System.Security.Claims;
using Funforum.Api.Common;
using Funforum.Api.Entities;
using Xunit;

namespace Funforum.Api.Tests.Common;

// roadmap.md GĐ3a: "Unit test bắt buộc: User B không thể edit/delete thread/reply của
// User A — viết ngay khi làm endpoint PUT/DELETE, không để sang GĐ8."
public class ResourceOwnershipTests
{
    private static ClaimsPrincipal UserPrincipal(int userId) =>
        new(new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim(Roles.ClaimType, Roles.User)]));

    private static ClaimsPrincipal AdminPrincipal(int adminId, bool superAdmin = false) =>
        new(new ClaimsIdentity([
            new Claim("sub", adminId.ToString()),
            new Claim(Roles.ClaimType, superAdmin ? Roles.SuperAdmin : Roles.Moderator),
        ]));

    [Fact]
    public void IsOwner_SameUser_ReturnsTrue()
    {
        var userA = UserPrincipal(1);
        Assert.True(ResourceOwnership.IsOwner(userA, authorUserId: 1, authorAdminId: null));
    }

    [Fact]
    public void IsOwner_DifferentUser_ReturnsFalse()
    {
        var userB = UserPrincipal(2);
        Assert.False(ResourceOwnership.IsOwner(userB, authorUserId: 1, authorAdminId: null));
    }

    [Fact]
    public void IsThreadOwner_UserBCannotEditUserAsThread()
    {
        var threadByA = new ForumThread { AuthorUserId = 1, AuthorAdminId = null };
        var userB = UserPrincipal(2);

        Assert.False(ResourceOwnership.IsThreadOwner(userB, threadByA));
    }

    [Fact]
    public void IsThreadOwner_AdminIsNotAutomaticallyOwner()
    {
        var threadByUser = new ForumThread { AuthorUserId = 1, AuthorAdminId = null };
        var admin = AdminPrincipal(1);

        Assert.False(ResourceOwnership.IsThreadOwner(admin, threadByUser));
    }

    [Fact]
    public void CanDeleteReply_ReplyAuthor_CanDelete()
    {
        var thread = new ForumThread { AuthorUserId = 99, AuthorAdminId = null };
        var reply = new Reply { AuthorUserId = 1, AuthorAdminId = null };
        var author = UserPrincipal(1);

        Assert.True(ResourceOwnership.CanDeleteReply(author, reply, thread));
    }

    [Fact]
    public void CanDeleteReply_ThreadOwner_CanDelete_EvenIfNotReplyAuthor()
    {
        var thread = new ForumThread { AuthorUserId = 1, AuthorAdminId = null };
        var reply = new Reply { AuthorUserId = 2, AuthorAdminId = null };
        var threadOwner = UserPrincipal(1);

        Assert.True(ResourceOwnership.CanDeleteReply(threadOwner, reply, thread));
    }

    [Fact]
    public void CanDeleteReply_Admin_CanDelete_EvenIfUnrelated()
    {
        var thread = new ForumThread { AuthorUserId = 1, AuthorAdminId = null };
        var reply = new Reply { AuthorUserId = 2, AuthorAdminId = null };
        var admin = AdminPrincipal(999);

        Assert.True(ResourceOwnership.CanDeleteReply(admin, reply, thread));
    }

    [Fact]
    public void CanDeleteReply_UnrelatedUser_CannotDelete()
    {
        var thread = new ForumThread { AuthorUserId = 1, AuthorAdminId = null };
        var reply = new Reply { AuthorUserId = 2, AuthorAdminId = null };
        var userC = UserPrincipal(3);

        Assert.False(ResourceOwnership.CanDeleteReply(userC, reply, thread));
    }
}
