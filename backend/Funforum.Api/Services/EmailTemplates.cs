namespace Funforum.Api.Services;

// Phase B — nội dung mail (tiếng Anh theo quy ước UI/API message). Link reset ghép từ
// Frontend:BaseUrl. Phase D tái dùng cho welcome khi tạo tài khoản bằng Google.
public static class EmailTemplates
{
    public static (string Subject, string Body) Welcome(string displayName) =>
        ("Welcome to Funforum",
            $"""
             Hi {displayName},

             Your Funforum account has been created and you can start posting right away.
             Your posts currently appear as "{displayName}" — you can change this in your account settings.

             — Funforum
             """);

    public static (string Subject, string Body) ResetPassword(string frontendBaseUrl, string rawToken)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        return ("Reset your Funforum password",
            $"""
             We received a request to reset your Funforum password.

             Open this link to choose a new password (valid for 30 minutes):
             {link}

             If you did not request this, you can safely ignore this email.

             — Funforum
             """);
    }
}
