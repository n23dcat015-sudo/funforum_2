using Microsoft.EntityFrameworkCore;
using Funforum.Api.Data;

namespace Funforum.Api.BackgroundJobs;

// current_state.md mục 4.1: đối soát score, reply_count, child_count ở tần suất thấp hơn
// hot_rank (mỗi giờ) để sửa sai lệch tích lũy từ các UPDATE nguyên tử (vote, thêm/xóa
// reply). 
public class ContentReconciliationService(
    IServiceScopeFactory scopeFactory, ILogger<ContentReconciliationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ContentReconciliationService failed to reconcile cached counters.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunforumDbContext>();

        // threads.score = SUM(votes.value) của thread đó, 0 nếu chưa ai vote.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE threads
            SET score = COALESCE((SELECT SUM(value) FROM votes WHERE votes.thread_id = threads.id), 0)
            """, ct);

        // replies.score = SUM(votes.value) của reply đó.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE replies
            SET score = COALESCE((SELECT SUM(value) FROM votes WHERE votes.reply_id = replies.id), 0)
            """, ct);

        // threads.reply_count = số reply đang hiển thị công khai (mục 3.3).
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE threads
            SET reply_count = COALESCE((
                SELECT COUNT(*) FROM replies
                WHERE replies.thread_id = threads.id
                  AND replies.status = 'visible' AND replies.hidden_ancestor_count = 0
            ), 0)
            """, ct);

        // replies.child_count = số reply cấp dưới đang hiển thị, gom theo root_reply_id
        // (mục 3.2/7.4). Chỉ có ý nghĩa với reply gốc (parent_reply_id IS NULL).
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE replies
            SET child_count = COALESCE((
                SELECT COUNT(*) FROM replies c
                WHERE c.root_reply_id = replies.id
                  AND c.status = 'visible' AND c.hidden_ancestor_count = 0
            ), 0)
            WHERE parent_reply_id IS NULL
            """, ct);

        // threads.image_count = số ảnh trong các reply đang hiển thị công khai, không tính
        // ảnh của bài mở đầu (mục 3.2). Bổ sung ở GĐ3d khi bảng attachments đã tồn tại.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE threads
            SET image_count = COALESCE((
                SELECT COUNT(*) FROM attachments a
                JOIN replies r ON r.id = a.reply_id
                WHERE r.thread_id = threads.id
                  AND r.status = 'visible' AND r.hidden_ancestor_count = 0
            ), 0)
            """, ct);

        logger.LogInformation("ContentReconciliationService reconciled score/reply_count/child_count.");
    }
}