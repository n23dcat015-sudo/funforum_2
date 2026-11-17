namespace Funforum.Api.Services;

// Phase B — trừu tượng gửi mail: dev/test ghi ra log, prod gửi SMTP (MailKit).
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
