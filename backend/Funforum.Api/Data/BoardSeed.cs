using Microsoft.EntityFrameworkCore;
using Funforum.Api.Entities;

namespace Funforum.Api.Data;

public static class BoardSeed
{
    private static readonly Board[] Boards =
    [
        new() { Slug = "general", Name = "General", Description = "General discussion", SortOrder = 1 },
        new() { Slug = "tech", Name = "Technology", Description = "Technology and programming", SortOrder = 2 },
        new() { Slug = "creative", Name = "Creative", Description = "Art, writing, and projects", SortOrder = 3 },
    ];

    public static async Task EnsureAsync(FunforumDbContext db, CancellationToken ct = default)
    {
        if (await db.Boards.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var board in Boards)
        {
            board.CreatedAt = now;
        }

        db.Boards.AddRange(Boards);
        await db.SaveChangesAsync(ct);
    }
}
