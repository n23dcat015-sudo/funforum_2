using Microsoft.EntityFrameworkCore;
using Funforum.Api.Entities;

namespace Funforum.Api.Common;

public static class EntityQueryExtensions
{
    public static IQueryable<ForumThread> Visible(this DbSet<ForumThread> source)
        => source.Where(t => t.Status == "visible");

    public static IQueryable<Reply> Visible(this DbSet<Reply> source)
        => source.Where(r => r.Status == "visible" && r.HiddenAncestorCount == 0);
}
