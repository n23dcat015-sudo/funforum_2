using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Votes;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public class VoteService(FunforumDbContext db) : IVoteService
{
    public async Task<VoteResultDto> SetThreadVoteAsync(
        int threadId, short value, ClaimsPrincipal user, CancellationToken ct)
    {
        ValidateValue(value);
        var userId = RequireUserId(user);

        var exists = await db.Threads.Visible().AnyAsync(t => t.Id == threadId, ct);
        if (!exists)
        {
            throw new ApiException(404, "Thread not found.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM threads WHERE id = {threadId} FOR UPDATE", ct);

        var delta = await UpsertVoteAsync(userId, threadId, null, value, ct);
        if (delta != 0)
        {
            await db.Threads.Where(t => t.Id == threadId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Score, t => t.Score + delta), ct);
        }

        await tx.CommitAsync(ct);

        var score = await db.Threads.Where(t => t.Id == threadId).Select(t => t.Score).SingleAsync(ct);
        return new VoteResultDto { Score = score, MyVote = value == 0 ? null : value };
    }

    public async Task<VoteResultDto> SetReplyVoteAsync(
        int replyId, short value, ClaimsPrincipal user, CancellationToken ct)
    {
        ValidateValue(value);
        var userId = RequireUserId(user);

        var exists = await db.Replies.Visible().AnyAsync(r => r.Id == replyId, ct);
        if (!exists)
        {
            throw new ApiException(404, "Reply not found.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM replies WHERE id = {replyId} FOR UPDATE", ct);

        var delta = await UpsertVoteAsync(userId, null, replyId, value, ct);
        if (delta != 0)
        {
            await db.Replies.Where(r => r.Id == replyId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Score, r => r.Score + delta), ct);
        }

        await tx.CommitAsync(ct);

        var score = await db.Replies.Where(r => r.Id == replyId).Select(r => r.Score).SingleAsync(ct);
        return new VoteResultDto { Score = score, MyVote = value == 0 ? null : value };
    }

    // current_state.md mục 4.1: thêm/sửa/xóa dòng votes rồi trả về delta để gọi ExecuteUpdate
    // nguyên tử trên score — không đọc-rồi-ghi. Idempotent: gọi lặp lại cùng value trả delta=0.
    private async Task<int> UpsertVoteAsync(
        int userId, int? threadId, int? replyId, short newValue, CancellationToken ct)
    {
        var existing = await db.Votes.SingleOrDefaultAsync(
            v => v.UserId == userId && v.ThreadId == threadId && v.ReplyId == replyId, ct);

        if (existing is null)
        {
            if (newValue == 0)
            {
                return 0; // bỏ vote nhưng chưa từng vote — không có gì để làm
            }

            db.Votes.Add(new Vote
            {
                UserId = userId,
                ThreadId = threadId,
                ReplyId = replyId,
                Value = newValue,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            return newValue;
        }

        if (newValue == existing.Value)
        {
            return 0; // idempotent
        }

        if (newValue == 0)
        {
            var deltaRemove = -(int)existing.Value;
            db.Votes.Remove(existing);
            await db.SaveChangesAsync(ct);
            return deltaRemove;
        }

        var delta = newValue - existing.Value;
        existing.Value = newValue;
        await db.SaveChangesAsync(ct);
        return delta;
    }

    private static void ValidateValue(short value)
    {
        if (value is not (-1 or 0 or 1))
        {
            throw new ApiException(400, "Vote value must be -1, 0, or 1.");
        }
    }

    // Chỉ user được vote; admin không vote (mục 4.1: "Chỉ user được vote (admin không vote)").
    private static int RequireUserId(ClaimsPrincipal user)
    {
        var role = Roles.Of(user);
        if (role != Roles.User)
        {
            throw new ApiException(403, "Only users can vote.");
        }
        return int.Parse(user.FindFirst("sub")!.Value);
    }
}