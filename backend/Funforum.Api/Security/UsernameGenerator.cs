using System.Security.Cryptography;

namespace Funforum.Api.Security;

// Phase A (auth_google_email_plan.md mục 3): username của user chỉ là handle kỹ thuật,
// tự sinh và không hiển thị (người dùng đăng nhập bằng email). Sinh dạng "anon_<hex 8>"
// để tránh đụng ReservedUsernames và luôn khớp unique index username.
public static class UsernameGenerator
{
    private const int HexBytes = 4; // 8 ký tự hex

    public static string Generate() =>
        $"anon_{Convert.ToHexString(RandomNumberGenerator.GetBytes(HexBytes)).ToLowerInvariant()}";
}
