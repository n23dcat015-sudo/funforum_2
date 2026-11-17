using Microsoft.EntityFrameworkCore;
using Funforum.Api.Data;

namespace Funforum.Api.BackgroundJobs;

// current_state.md mục 10 "Dọn dữ liệu": xóa notification cũ hơn 30 ngày, cùng nhịp với
// job dọn file mồ côi (OrphanFileCleanupService, GĐ3d).
public class NotificationCleanupService(
    IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "NotificationCleanupService failed to clean up old notifications.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunforumDbContext>();

        var cutoff = DateTime.UtcNow - MaxAge;
        var deleted = await db.Notifications.Where(n => n.CreatedAt < cutoff).ExecuteDeleteAsync(ct);

        logger.LogInformation("NotificationCleanupService deleted {Count} old notifications.", deleted);
    }
}