namespace Funforum.Api.Common;

// Phase C/D — cấu hình frontend (section "Frontend"). Dùng để ghép link reset-password
// trong mail (auth_google_email_plan.md §5).
public class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; set; } = "http://localhost:5173";
}
