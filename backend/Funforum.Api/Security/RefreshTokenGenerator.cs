using System.Security.Cryptography;
using System.Text;

namespace Funforum.Api.Security;

// Token thô chỉ tồn tại trong cookie phía client; DB chỉ lưu hash (mục 3.2).
public static class RefreshTokenGenerator
{
    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bit
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant(); // 64 ký tự hex, khớp varchar(64)
    }
}
