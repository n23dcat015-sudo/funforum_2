using Funforum.Api.Common;
using Xunit;

namespace Funforum.Api.Tests.Common;

// Phase A/F: sau khi mọi user đều hiển thị "Anonymous", quyền sở hữu phải dựa vào id chứ
// không so chuỗi tên (auth_google_email_plan.md mục 1.4). ViewerContext là nguồn tính cờ
// isMine/canDelete trên DTO công khai.
public class ViewerContextTests
{
    [Fact]
    public void AnonymousViewer_IsAuthorOfNobody()
    {
        Assert.False(ViewerContext.Anonymous.IsAuthor(authorUserId: 1, authorAdminId: null));
        Assert.False(ViewerContext.Anonymous.IsAuthor(authorUserId: null, authorAdminId: 2));
    }

    [Fact]
    public void TwoAnonymousUsers_SameDisplayName_IsNotOwnerOfEachOther()
    {
        // Cả hai đều hiển thị "Anonymous" (tên không còn phân biệt được) nhưng id khác nhau.
        var userA = new ViewerContext(UserId: 1, AdminId: null, IsAdmin: false);
        var userB = new ViewerContext(UserId: 2, AdminId: null, IsAdmin: false);

        Assert.True(userA.IsAuthor(1, null));
        Assert.False(userB.IsAuthor(1, null));
        Assert.True(userB.IsAuthor(2, null));
        Assert.False(userA.IsAuthor(2, null));
    }

    [Fact]
    public void UserIsNotOwnerOfAdminPost_AndViceVersa()
    {
        // users.id và admins.id là hai dãy độc lập: trùng số không có nghĩa là cùng người.
        var user = new ViewerContext(UserId: 5, AdminId: null, IsAdmin: false);
        var admin = new ViewerContext(UserId: null, AdminId: 5, IsAdmin: true);

        Assert.False(user.IsAuthor(null, 5));
        Assert.False(admin.IsAuthor(5, null));
    }

    [Fact]
    public void From_UnauthenticatedRequest_IsAnonymous()
    {
        Assert.Equal(ViewerContext.Anonymous, ViewerContext.From(null));
    }
}
