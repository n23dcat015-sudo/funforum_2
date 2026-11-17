namespace Funforum.Api.Common;

// Phase B — cấu hình SMTP cho production (section "Email"). Dev/test để Host rỗng thì
// Program.cs đăng ký LogEmailSender thay vì SmtpEmailSender.
public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "Funforum";
    public bool EnableSsl { get; set; } = true;
}
