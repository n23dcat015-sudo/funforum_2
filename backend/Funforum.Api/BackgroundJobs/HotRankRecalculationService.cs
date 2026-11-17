using Microsoft.EntityFrameworkCore;
using Funforum.Api.Data;

namespace Funforum.Api.BackgroundJobs;

// current_state.md mục 4.1: hot_rank = score / (giờ_đã_trôi + 2)^1.5 (kiểu Hacker News),
// tính theo threads.created_at, chạy mỗi vài phút.
public class HotRankRecalculationService(
    IServiceScopeFactory scopeFactory, ILogger<HotRankRecalculationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await RecalculateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // mục 11.9: job nền phải try/catch + log có cấu trúc, không làm gián đoạn app.
                logger.LogError(ex, "HotRankRecalculationService failed to recalculate hot_rank.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RecalculateAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunforumDbContext>();

        var rows = await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE threads
            SET hot_rank = score / power(
                EXTRACT(EPOCH FROM (now() - created_at)) / 3600.0 + 2, 1.5)
            WHERE status = 'visible'
            """, ct);

        logger.LogInformation("HotRankRecalculationService updated hot_rank for {Rows} threads.", rows);
    }
}