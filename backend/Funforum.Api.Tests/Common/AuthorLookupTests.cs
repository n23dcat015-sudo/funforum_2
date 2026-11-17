using Funforum.Api.Common;
using Xunit;

namespace Funforum.Api.Tests.Common;

public class AuthorLookupTests
{
    [Fact]
    public void ResolvePublicName_AdminWithoutCapcode_HidesUsername()
    {
        var admins = new Dictionary<int, string> { [7] = "real-admin-name" };

        var name = AuthorLookup.ResolvePublicName(
            userId: null,
            adminId: 7,
            capcode: null,
            users: new Dictionary<int, string>(),
            admins);

        Assert.Equal("Anonymous", name);
        Assert.DoesNotContain("real-admin-name", name);
    }

    [Fact]
    public void ResolvePublicName_AdminWithCapcode_PreservesCapcodeAuthorLabel()
    {
        var admins = new Dictionary<int, string> { [7] = "admin" };

        var name = AuthorLookup.ResolvePublicName(
            userId: null,
            adminId: 7,
            capcode: "admin",
            users: new Dictionary<int, string>(),
            admins);

        Assert.Equal("admin", name);
    }
}
