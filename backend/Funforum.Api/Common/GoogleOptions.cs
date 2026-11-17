namespace Funforum.Api.Common;

// Phase D — cấu hình Google Sign-In (section "Google"). ClientId phải trùng
// VITE_GOOGLE_CLIENT_ID của frontend (auth_google_email_plan.md §5).
public class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
}
