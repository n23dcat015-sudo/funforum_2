using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Entities;

namespace Funforum.Api.Data;

public class FunforumDbContext(DbContextOptions<FunforumDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // GĐ3a — current_state.md mục 3.2. Không gọi ToTable() nên tên bảng lấy theo tên
    // DbSet (snake_case hóa): "boards", "threads", "replies", "votes".
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Attachment> Attachments => Set<Attachment>(); // GĐ3d
    public DbSet<Notification> Notifications => Set<Notification>(); // GĐ3h

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>(); // X.2

    public DbSet<Report> Reports => Set<Report>(); // X.3
    public DbSet<Ban> Bans => Set<Ban>(); // X.4
    public DbSet<BanAppeal> BanAppeals => Set<BanAppeal>(); // X.5

    // Phase A (auth_google_email_plan.md mục 2.1): danh tính bên thứ ba + token đặt lại mật khẩu.
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // GĐ1 quy định migration đầu tiên chỉ bật 2 extension này để kiểm tra
        // kết nối/quyền DB. Vì GĐ1 chưa chạy được ở môi trường tạo dự án (không
        // có dotnet SDK), khai báo được gộp vào migration AddAuthTables của GĐ2 —
        // xem README/hướng dẫn migrate.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).HasColumnType("citext").IsRequired();
            entity.Property(u => u.Email).HasColumnType("citext").IsRequired();
            // Phase A: hiển thị công khai, KHÔNG unique; mặc định "Anonymous".
            entity.Property(u => u.DisplayName).HasColumnType("citext").HasDefaultValue("Anonymous").IsRequired();
            // Phase A: NULL với tài khoản chỉ đăng nhập bằng Google.
            entity.Property(u => u.PasswordHash).HasMaxLength(100);
            entity.Property(u => u.FullName).HasMaxLength(100);
            entity.Property(u => u.Address).HasMaxLength(255);
            entity.Property(u => u.Phone).HasMaxLength(20);

            // X.4 — trạng thái tài khoản, dùng để chặn POST khi bị ban.
            entity.Property(u => u.Status).HasMaxLength(20).HasDefaultValue(UserStatuses.Active);
            entity.HasCheckConstraint(
                "ck_users_status",
                $"status IN ('{UserStatuses.Active}','{UserStatuses.Banned}','{UserStatuses.BannedPermanently}')");

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        ConfigureUserIdentities(modelBuilder);
        ConfigurePasswordResetTokens(modelBuilder);

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.Property(a => a.Username).HasColumnType("citext").IsRequired();
            entity.Property(a => a.Email).HasColumnType("citext").IsRequired();
            entity.Property(a => a.PasswordHash).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Role).HasMaxLength(12).IsRequired();

            entity.HasIndex(a => a.Username).IsUnique();
            entity.HasIndex(a => a.Email).IsUnique();

            // X.1: role 3 cấp thay cho is_super_admin.
            entity.ToTable("admins");
            entity.HasCheckConstraint(
                "ck_admins_role",
                "role IN ('janitor','moderator','super_admin')");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(rt => rt.TokenHash).IsUnique();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(rt => rt.AdminId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Đúng 1 trong 2 cột user_id/admin_id có giá trị — current_state.md mục 3.2.
            entity.ToTable("refresh_tokens");
            entity.HasCheckConstraint(
                "ck_refresh_tokens_user_xor_admin",
                "(user_id IS NULL) <> (admin_id IS NULL)");

            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.AdminId);
        });

        ConfigureBoards(modelBuilder);
        ConfigureThreads(modelBuilder);
        ConfigureReplies(modelBuilder);
        ConfigureVotes(modelBuilder);
        ConfigureAttachments(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureReports(modelBuilder);
        ConfigureBans(modelBuilder);
        ConfigureBanAppeals(modelBuilder);
    }

    // --- Phase A: user_identities / password_reset_tokens — auth_google_email_plan.md mục 2.1 ---

    private static void ConfigureUserIdentities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserIdentity>(entity =>
        {
            entity.Property(i => i.Provider).HasMaxLength(20).IsRequired();
            entity.Property(i => i.ProviderUserId).IsRequired();
            entity.Property(i => i.ProviderEmail).HasColumnType("citext").IsRequired();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Một định danh provider chỉ liên kết với đúng 1 user.
            entity.HasIndex(i => new { i.Provider, i.ProviderUserId }).IsUnique();
            entity.HasIndex(i => i.UserId);

            entity.ToTable("user_identities");
            entity.HasCheckConstraint("ck_user_identities_provider", "provider IN ('google')");
        });
    }

    private static void ConfigurePasswordResetTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.Property(t => t.TokenHash).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => t.UserId);

            entity.ToTable("password_reset_tokens");
        });
    }

    // --- GĐ3a: boards / threads / replies / votes — current_state.md mục 3.2–3.5 ---

    private static void ConfigureBoards(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(entity =>
        {
            entity.Property(b => b.Slug).HasMaxLength(50).IsRequired();
            entity.Property(b => b.Name).HasMaxLength(ContentLimits.BoardNameMax).IsRequired();
            entity.Property(b => b.Description).HasMaxLength(ContentLimits.BoardDescriptionMax);
            entity.Property(b => b.SortOrder).HasDefaultValue(0);

            entity.HasIndex(b => b.Slug).IsUnique();

            // mục 3.5: định dạng slug bắt buộc ở tầng DB để seed/script không chèn sai được.
            entity.ToTable("boards");
            entity.HasCheckConstraint(
                "ck_boards_slug_format",
                "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 2 AND 50");
        });
    }

    private static void ConfigureThreads(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ForumThread>(entity =>
        {
            entity.Property(t => t.Title).HasMaxLength(ContentLimits.ThreadTitleMax).IsRequired();
            entity.Property(t => t.Content).IsRequired();
            entity.Property(t => t.Status).HasMaxLength(10).HasDefaultValue("visible");
            entity.Property(t => t.Capcode).HasMaxLength(12);
            entity.Property(t => t.PosterIp).HasMaxLength(45); // X.4
            entity.Property(t => t.Score).HasDefaultValue(0);
            entity.Property(t => t.HotRank).HasDefaultValue(0d);
            entity.Property(t => t.ReplyCount).HasDefaultValue(0);
            entity.Property(t => t.ImageCount).HasDefaultValue(0);
            entity.Property(t => t.ImagesPosted).HasDefaultValue(0);

            entity.HasOne<Board>()
                  .WithMany()
                  .HasForeignKey(t => t.BoardId)
                  .OnDelete(DeleteBehavior.Restrict); // mục 3.3: chặn xóa board còn thread

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(t => t.AuthorUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(t => t.AuthorAdminId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.ToTable("threads");
            entity.HasCheckConstraint("ck_threads_author_xor", "author_user_id IS NULL OR author_admin_id IS NULL");
            // X.1: capcode lấy nguyên văn từ role của admin (AuthorContext.ResolveCapcode) nên
            // danh sách này phải khớp đúng 3 giá trị role.
            entity.HasCheckConstraint("ck_threads_capcode", "capcode IS NULL OR capcode IN ('janitor','moderator','super_admin')");
            entity.HasCheckConstraint("ck_threads_status", "status IN ('visible','hidden')");
            entity.HasCheckConstraint(
                "ck_threads_title_length",
                $"char_length(title) BETWEEN 1 AND {ContentLimits.ThreadTitleMax}");
            entity.HasCheckConstraint(
                "ck_threads_content_length",
                $"char_length(content) BETWEEN 1 AND {ContentLimits.ThreadContentMax}");

            // mục 3.4 — index một phần cho feed (chỉ thread visible), id làm khóa phụ.
            entity.HasIndex(t => new { t.BoardId, t.CreatedAt, t.Id })
                  .HasDatabaseName("ix_threads_board_new")
                  .IsDescending(false, true, true)
                  .HasFilter("status = 'visible'");
            entity.HasIndex(t => new { t.BoardId, t.Score, t.Id })
                  .HasDatabaseName("ix_threads_board_top")
                  .IsDescending(false, true, true)
                  .HasFilter("status = 'visible'");
            entity.HasIndex(t => new { t.BoardId, t.HotRank, t.Id })
                  .HasDatabaseName("ix_threads_board_hot")
                  .IsDescending(false, true, true)
                  .HasFilter("status = 'visible'");
            entity.HasIndex(t => new { t.BoardId, t.LastReplyAt, t.Id })
                  .HasDatabaseName("ix_threads_board_latest") // mục 4.4, dùng đầy đủ ở GĐ3f
                  .IsDescending(false, true, true)
                  .HasFilter("status = 'visible'");

            // GIN trigram cho search (mục 4.3). CHƯA build/verify được ở môi trường viết code
            // này (không có dotnet SDK) — kiểm tra lại cú pháp HasMethod/HasOperators khi chạy
            // `dotnet ef migrations add` với Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10.
            entity.HasIndex(t => t.Title)
                  .HasDatabaseName("ix_threads_title_trgm")
                  .HasMethod("gin")
                  .HasOperators("gin_trgm_ops");
            entity.HasIndex(t => t.Content)
                  .HasDatabaseName("ix_threads_content_trgm")
                  .HasMethod("gin")
                  .HasOperators("gin_trgm_ops");
        });
    }

    private static void ConfigureReplies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reply>(entity =>
        {
            entity.Property(r => r.Content).IsRequired();
            entity.Property(r => r.Status).HasMaxLength(10).HasDefaultValue("visible");
            entity.Property(r => r.HiddenBy).HasMaxLength(10);
            entity.Property(r => r.Capcode).HasMaxLength(12);
            entity.Property(r => r.PosterIp).HasMaxLength(45); // X.4
            entity.Property(r => r.HiddenAncestorCount).HasDefaultValue(0);
            entity.Property(r => r.ChildCount).HasDefaultValue(0);
            entity.Property(r => r.Score).HasDefaultValue(0);

            entity.HasOne<ForumThread>()
                  .WithMany()
                  .HasForeignKey(r => r.ThreadId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Reply>()
                  .WithMany()
                  .HasForeignKey(r => r.ParentReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Reply>()
                  .WithMany()
                  .HasForeignKey(r => r.RootReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(r => r.AuthorUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(r => r.AuthorAdminId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.ToTable("replies");
            entity.HasCheckConstraint("ck_replies_author_xor", "author_user_id IS NULL OR author_admin_id IS NULL");
            entity.HasCheckConstraint("ck_replies_capcode", "capcode IS NULL OR capcode IN ('janitor','moderator','super_admin')");
            entity.HasCheckConstraint("ck_replies_status", "status IN ('visible','hidden')");
            entity.HasCheckConstraint("ck_replies_hidden_by", "hidden_by IS NULL OR hidden_by IN ('owner','admin')");
            entity.HasCheckConstraint(
                "ck_replies_content_length",
                $"char_length(content) BETWEEN 0 AND {ContentLimits.ReplyContentMax}");

            entity.HasIndex(r => new { r.ThreadId, r.CreatedAt }).HasDatabaseName("ix_replies_thread_created");
            entity.HasIndex(r => new { r.RootReplyId, r.CreatedAt }).HasDatabaseName("ix_replies_root_created");
        });
    }

    private static void ConfigureVotes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.Property(v => v.Value).IsRequired();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(v => v.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ForumThread>()
                  .WithMany()
                  .HasForeignKey(v => v.ThreadId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Reply>()
                  .WithMany()
                  .HasForeignKey(v => v.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("votes");
            entity.HasCheckConstraint("ck_votes_value", "value IN (-1, 1)");
            entity.HasCheckConstraint("ck_votes_target_xor", "(thread_id IS NULL) <> (reply_id IS NULL)");

            // mục 3.2: chặn vote 2 lần bằng 2 unique index một phần.
            entity.HasIndex(v => new { v.UserId, v.ThreadId })
                  .HasDatabaseName("ux_votes_user_thread")
                  .IsUnique()
                  .HasFilter("thread_id IS NOT NULL");
            entity.HasIndex(v => new { v.UserId, v.ReplyId })
                  .HasDatabaseName("ux_votes_user_reply")
                  .IsUnique()
                  .HasFilter("reply_id IS NOT NULL");
        });
    }

    private static void ConfigureAttachments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.Property(a => a.StoredName).HasMaxLength(64).IsRequired();
            entity.Property(a => a.ThumbName).HasMaxLength(64).IsRequired();
            entity.Property(a => a.OriginalName).HasMaxLength(255).IsRequired();
            entity.Property(a => a.MimeType).HasMaxLength(20).IsRequired();

            entity.HasIndex(a => a.StoredName).IsUnique();

            entity.HasOne<ForumThread>()
                  .WithMany()
                  .HasForeignKey(a => a.ThreadId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Reply>()
                  .WithMany()
                  .HasForeignKey(a => a.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("attachments");
            entity.HasCheckConstraint(
                "ck_attachments_target_xor",
                "(thread_id IS NULL) <> (reply_id IS NULL)");

            // mục 3.7: mỗi thread/reply tối đa 1 ảnh — 2 unique index một phần.
            entity.HasIndex(a => a.ThreadId)
                  .HasDatabaseName("ux_attachments_thread")
                  .IsUnique()
                  .HasFilter("thread_id IS NOT NULL");
            entity.HasIndex(a => a.ReplyId)
                  .HasDatabaseName("ux_attachments_reply")
                  .IsUnique()
                  .HasFilter("reply_id IS NOT NULL");
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne<Reply>()
                  .WithMany()
                  .HasForeignKey(n => n.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(n => n.RecipientUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(n => n.RecipientAdminId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("notifications");
            entity.HasCheckConstraint(
                "ck_notifications_recipient_xor",
                "(recipient_user_id IS NULL) <> (recipient_admin_id IS NULL)");

            entity.HasIndex(n => new { n.RecipientUserId, n.CreatedAt })
                  .HasDatabaseName("ix_notifications_user_created");
            entity.HasIndex(n => new { n.RecipientAdminId, n.CreatedAt })
                  .HasDatabaseName("ix_notifications_admin_created");
        });
    }

    // --- X.2: audit_logs ---

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(l => l.Action).HasMaxLength(24).IsRequired();
            entity.Property(l => l.TargetType).HasMaxLength(16).IsRequired();
            entity.Property(l => l.Reason).HasMaxLength(500);
            entity.Property(l => l.Metadata).HasColumnType("jsonb");

            // SET NULL chứ không Cascade: admin bị xoá thì hành động vẫn phải còn dấu vết.
            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(l => l.ActorAdminId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => new { l.ActorAdminId, l.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(l => new { l.Action, l.CreatedAt });
            entity.HasIndex(l => new { l.TargetType, l.TargetId });

            entity.ToTable("audit_logs");
            entity.HasCheckConstraint(
                "ck_audit_logs_action",
                "action IN ('hide','unhide','delete','ban','unban','role_change'," +
                "'board_create','board_delete','wordfilter_edit')");
            entity.HasCheckConstraint(
                "ck_audit_logs_target_type",
                "target_type IN ('thread','reply','board','admin','user')");
        });
    }

    // --- X.3: reports ---

    private static void ConfigureReports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Report>(entity =>
        {
            entity.Property(r => r.TargetType).HasMaxLength(16).IsRequired();
            entity.Property(r => r.Reason).HasMaxLength(ContentLimits.ReportReasonMax).IsRequired();
            entity.Property(r => r.Status).HasMaxLength(16).HasDefaultValue(ReportStatuses.Pending);

            // SET NULL: người report bị xoá thì report vẫn còn để xử lý.
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(r => r.ReporterUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => new { r.Status, r.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(r => new { r.TargetType, r.TargetId });

            entity.ToTable("reports");
            entity.HasCheckConstraint(
                "ck_reports_target_type",
                "target_type IN ('thread','reply')");
            entity.HasCheckConstraint(
                "ck_reports_status",
                "status IN ('pending','escalated','resolved')");
        });
    }

    // --- X.4: bans ---

    private static void ConfigureBans(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ban>(entity =>
        {
            entity.Property(b => b.BannedIp).HasMaxLength(45);
            entity.Property(b => b.Reason).HasMaxLength(ContentLimits.BanReasonMax);
            entity.Property(b => b.Status).HasMaxLength(16).HasDefaultValue(BanStatuses.Active);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // SET NULL: admin bị xoá thì bản ghi ban vẫn còn dấu vết.
            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(b => b.BannedByAdminId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(b => new { b.Status, b.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(b => b.UserId);
            // Tra IP đang bị ban khi đăng ký/đăng bài (X.4).
            entity.HasIndex(b => b.BannedIp).HasFilter("banned_ip IS NOT NULL AND status = 'active'");

            entity.ToTable("bans");
            entity.HasCheckConstraint(
                "ck_bans_status",
                "status IN ('active','lifted')");
        });
    }

    // --- X.5: ban_appeals ---

    private static void ConfigureBanAppeals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BanAppeal>(entity =>
        {
            entity.Property(a => a.ReasonText).HasMaxLength(ContentLimits.AppealReasonMax).IsRequired();
            entity.Property(a => a.Status).HasMaxLength(16).HasDefaultValue(AppealStatuses.Pending);

            entity.HasOne<Ban>()
                  .WithMany()
                  .HasForeignKey(a => a.BanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // SET NULL: admin duyệt bị xoá thì đơn kháng cáo vẫn còn dấu vết.
            entity.HasOne<Admin>()
                  .WithMany()
                  .HasForeignKey(a => a.ReviewedByAdminId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(a => new { a.Status, a.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(a => a.BanId);
            entity.HasIndex(a => a.UserId);

            entity.ToTable("ban_appeals");
            entity.HasCheckConstraint(
                "ck_ban_appeals_status",
                "status IN ('pending','rejected','approved')");
            entity.HasCheckConstraint(
                "ck_ban_appeals_reason_length",
                $"char_length(reason_text) BETWEEN 1 AND {ContentLimits.AppealReasonMax}");
        });
    }
}