using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;

namespace Funforum.Api.BackgroundJobs;

// current_state.md mục 3.7 "Xóa file": dọn file trong thư mục upload không còn được
// attachments tham chiếu, cũ hơn 1 giờ (tránh xóa nhầm file đang upload dở).
public class OrphanFileCleanupService(
    IServiceScopeFactory scopeFactory, UploadPaths paths, ILogger<OrphanFileCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);
    private static readonly TimeSpan MinAge = TimeSpan.FromHours(1);

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
                logger.LogError(ex, "OrphanFileCleanupService failed to clean up orphan files.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunforumDbContext>();

        var referencedImages = (await db.Attachments.Select(a => a.StoredName).ToListAsync(ct)).ToHashSet();
        var referencedThumbs = (await db.Attachments.Select(a => a.ThumbName).ToListAsync(ct)).ToHashSet();

        var deleted = CleanDirectory(paths.ImagesDir, referencedImages)
                    + CleanDirectory(paths.ThumbsDir, referencedThumbs);

        logger.LogInformation("OrphanFileCleanupService deleted {Count} orphan files.", deleted);
    }

    private static int CleanDirectory(string dir, HashSet<string> referenced)
    {
        var count = 0;
        var cutoff = DateTime.UtcNow - MinAge;

        foreach (var path in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(path);
            if (referenced.Contains(name) || File.GetLastWriteTimeUtc(path) > cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                count++;
            }
            catch (IOException)
            {
                // thử lại ở lần chạy sau
            }
        }

        return count;
    }
}