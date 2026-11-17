using Microsoft.Extensions.Logging;

namespace Funforum.Api.Services;

// Phase B — Development/test (hoặc khi chưa cấu hình SMTP): không gửi thật, chỉ ghi nội
// dung mail ra log để dev thấy được link/token.
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email (dev, not actually sent) → To: {To} | Subject: {Subject}\n{Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
