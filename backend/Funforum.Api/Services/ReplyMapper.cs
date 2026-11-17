using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Replies;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

internal static class ReplyMapper
{
    private sealed class AuthorRef
    {
        public int? UserId { get; init; }
        public int? AdminId { get; init; }
        public string? Capcode { get; init; }
    }

    private sealed class ThreadOwner
    {
        public int Id { get; init; }
        public int? UserId { get; init; }
        public int? AdminId { get; init; }
    }

    public static async Task<List<ReplyDto>> MapAsync(
        FunforumDbContext db, List<Reply> replies, ViewerContext viewer, CancellationToken ct)
    {
        // mục 7.4: @username của reply cấp 2 = tác giả parent_reply_id, khi khác tác giả
        // reply gốc của nhánh. Nếu parent/root không nằm trong chính `replies` đang map
        // (ví dụ chỉ map 1 trang children), tra thêm tác giả của chúng riêng.
        var referenceIds = replies
            .Where(r => r.RootReplyId != null)
            .SelectMany(r => new[] { r.ParentReplyId, r.RootReplyId })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Except(replies.Select(r => r.Id))
            .Distinct()
            .ToList();

        var referenceAuthors = referenceIds.Count == 0
            ? new Dictionary<int, AuthorRef>()
            : await db.Replies.Where(r => referenceIds.Contains(r.Id))
                .Select(r => new { r.Id, r.AuthorUserId, r.AuthorAdminId, r.Capcode })
                .ToDictionaryAsync(
                    r => r.Id,
                    r => new AuthorRef { UserId = r.AuthorUserId, AdminId = r.AuthorAdminId, Capcode = r.Capcode },
                    ct);

        var allUserIds = replies.Where(r => r.AuthorUserId != null).Select(r => r.AuthorUserId!.Value)
            .Concat(referenceAuthors.Values.Where(a => a.UserId != null).Select(a => a.UserId!.Value));
        var allAdminIds = replies.Where(r => r.AuthorAdminId != null).Select(r => r.AuthorAdminId!.Value)
            .Concat(referenceAuthors.Values.Where(a => a.AdminId != null).Select(a => a.AdminId!.Value));

        var users = await AuthorLookup.GetDisplayNamesAsync(db, allUserIds, ct);
        var admins = await AuthorLookup.GetAdminUsernamesAsync(db, allAdminIds, ct);

        // Phase A: CanDelete của reply phụ thuộc chủ thread (mục 3.3), mà một lần map có thể
        // gồm reply của nhiều thread (preview ở board index) → tra chủ sở hữu theo lô.
        var threadIds = replies.Select(r => r.ThreadId).Distinct().ToList();
        var threadOwners = await db.Threads
            .Where(t => threadIds.Contains(t.Id))
            .Select(t => new ThreadOwner { Id = t.Id, UserId = t.AuthorUserId, AdminId = t.AuthorAdminId })
            .ToDictionaryAsync(t => t.Id, ct);

        var replyIds = replies.Select(r => r.Id).ToList();
        var attachments = await db.Attachments
            .Where(a => a.ReplyId != null && replyIds.Contains(a.ReplyId!.Value))
            .ToDictionaryAsync(a => a.ReplyId!.Value, a => a, ct);

        string? NameOf(int id)
        {
            var self = replies.SingleOrDefault(r => r.Id == id);
            if (self is not null)
            {
                return AuthorLookup.ResolvePublicName(self.AuthorUserId, self.AuthorAdminId, self.Capcode, users, admins);
            }
            return referenceAuthors.TryGetValue(id, out var a)
                ? AuthorLookup.ResolvePublicName(a.UserId, a.AdminId, a.Capcode, users, admins)
                : null;
        }

        return replies.Select(r =>
        {
            string? replyToUsername = null;
            if (r.RootReplyId is int rootId && r.ParentReplyId is int parentId)
            {
                var parentName = NameOf(parentId);
                var rootName = NameOf(rootId);
                if (parentName is not null && parentName != rootName)
                {
                    replyToUsername = parentName;
                }
            }

            return new ReplyDto
            {
                Id = r.Id,
                ThreadId = r.ThreadId,
                ParentReplyId = r.ParentReplyId,
                RootReplyId = r.RootReplyId,
                Content = r.Content,
                AuthorName = AuthorLookup.ResolvePublicName(r.AuthorUserId, r.AuthorAdminId, r.Capcode, users, admins),
                Capcode = r.Capcode,
                IsMine = viewer.IsAuthor(r.AuthorUserId, r.AuthorAdminId),
                CanDelete = viewer.IsAdmin
                    || viewer.IsAuthor(r.AuthorUserId, r.AuthorAdminId)
                    || (threadOwners.TryGetValue(r.ThreadId, out var owner) && viewer.IsAuthor(owner.UserId, owner.AdminId)),
                ChildCount = r.ChildCount,
                Score = r.Score,
                Status = r.Status,
                HiddenBy = r.HiddenBy,
                IsHidden = r.Status != "visible" || r.HiddenAncestorCount > 0,
                Image = attachments.TryGetValue(r.Id, out var att) ? AttachmentMapper.ToDto(att) : null,
                ReplyToUsername = replyToUsername,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            };
        }).ToList();
    }
}