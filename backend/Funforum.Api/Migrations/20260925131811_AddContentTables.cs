using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Funforum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boards",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boards", x => x.id);
                    table.CheckConstraint("ck_boards_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 2 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "threads",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    board_id = table.Column<int>(type: "integer", nullable: false),
                    author_user_id = table.Column<int>(type: "integer", nullable: true),
                    author_admin_id = table.Column<int>(type: "integer", nullable: true),
                    capcode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "visible"),
                    score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hot_rank = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    reply_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    image_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    images_posted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_reply_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_threads", x => x.id);
                    table.CheckConstraint("ck_threads_author_xor", "author_user_id IS NULL OR author_admin_id IS NULL");
                    table.CheckConstraint("ck_threads_capcode", "capcode IS NULL OR capcode IN ('admin','superadmin')");
                    table.CheckConstraint("ck_threads_content_length", "char_length(content) BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_threads_status", "status IN ('visible','hidden')");
                    table.CheckConstraint("ck_threads_title_length", "char_length(title) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "fk_threads_admins_author_admin_id",
                        column: x => x.author_admin_id,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_threads_boards_board_id",
                        column: x => x.board_id,
                        principalTable: "boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_threads_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "replies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    thread_id = table.Column<int>(type: "integer", nullable: false),
                    parent_reply_id = table.Column<int>(type: "integer", nullable: true),
                    root_reply_id = table.Column<int>(type: "integer", nullable: true),
                    author_user_id = table.Column<int>(type: "integer", nullable: true),
                    author_admin_id = table.Column<int>(type: "integer", nullable: true),
                    capcode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "visible"),
                    hidden_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    hidden_ancestor_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    child_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_replies", x => x.id);
                    table.CheckConstraint("ck_replies_author_xor", "author_user_id IS NULL OR author_admin_id IS NULL");
                    table.CheckConstraint("ck_replies_capcode", "capcode IS NULL OR capcode IN ('admin','superadmin')");
                    table.CheckConstraint("ck_replies_content_length", "char_length(content) BETWEEN 0 AND 5000");
                    table.CheckConstraint("ck_replies_hidden_by", "hidden_by IS NULL OR hidden_by IN ('owner','admin')");
                    table.CheckConstraint("ck_replies_status", "status IN ('visible','hidden')");
                    table.ForeignKey(
                        name: "fk_replies_admins_author_admin_id",
                        column: x => x.author_admin_id,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_replies_replies_parent_reply_id",
                        column: x => x.parent_reply_id,
                        principalTable: "replies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_replies_replies_root_reply_id",
                        column: x => x.root_reply_id,
                        principalTable: "replies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_replies_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_replies_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    thread_id = table.Column<int>(type: "integer", nullable: true),
                    reply_id = table.Column<int>(type: "integer", nullable: true),
                    stored_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    thumb_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_target_xor", "(thread_id IS NULL) <> (reply_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_attachments_replies_reply_id",
                        column: x => x.reply_id,
                        principalTable: "replies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_admin_id = table.Column<int>(type: "integer", nullable: true),
                    reply_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_recipient_xor", "(recipient_user_id IS NULL) <> (recipient_admin_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_notifications_admins_recipient_admin_id",
                        column: x => x.recipient_admin_id,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_replies_reply_id",
                        column: x => x.reply_id,
                        principalTable: "replies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    thread_id = table.Column<int>(type: "integer", nullable: true),
                    reply_id = table.Column<int>(type: "integer", nullable: true),
                    value = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_votes", x => x.id);
                    table.CheckConstraint("ck_votes_target_xor", "(thread_id IS NULL) <> (reply_id IS NULL)");
                    table.CheckConstraint("ck_votes_value", "value IN (-1, 1)");
                    table.ForeignKey(
                        name: "fk_votes_replies_reply_id",
                        column: x => x.reply_id,
                        principalTable: "replies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_votes_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_votes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_stored_name",
                table: "attachments",
                column: "stored_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attachments_reply",
                table: "attachments",
                column: "reply_id",
                unique: true,
                filter: "reply_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_attachments_thread",
                table: "attachments",
                column: "thread_id",
                unique: true,
                filter: "thread_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_boards_slug",
                table: "boards",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_admin_created",
                table: "notifications",
                columns: new[] { "recipient_admin_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_reply_id",
                table: "notifications",
                column: "reply_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_created",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_replies_author_admin_id",
                table: "replies",
                column: "author_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_replies_author_user_id",
                table: "replies",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_replies_parent_reply_id",
                table: "replies",
                column: "parent_reply_id");

            migrationBuilder.CreateIndex(
                name: "ix_replies_root_created",
                table: "replies",
                columns: new[] { "root_reply_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_replies_thread_created",
                table: "replies",
                columns: new[] { "thread_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_threads_author_admin_id",
                table: "threads",
                column: "author_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_threads_author_user_id",
                table: "threads",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_threads_board_hot",
                table: "threads",
                columns: new[] { "board_id", "hot_rank", "id" },
                descending: new[] { false, true, true },
                filter: "status = 'visible'");

            migrationBuilder.CreateIndex(
                name: "ix_threads_board_latest",
                table: "threads",
                columns: new[] { "board_id", "last_reply_at", "id" },
                descending: new[] { false, true, true },
                filter: "status = 'visible'");

            migrationBuilder.CreateIndex(
                name: "ix_threads_board_new",
                table: "threads",
                columns: new[] { "board_id", "created_at", "id" },
                descending: new[] { false, true, true },
                filter: "status = 'visible'");

            migrationBuilder.CreateIndex(
                name: "ix_threads_board_top",
                table: "threads",
                columns: new[] { "board_id", "score", "id" },
                descending: new[] { false, true, true },
                filter: "status = 'visible'");

            migrationBuilder.CreateIndex(
                name: "ix_threads_content_trgm",
                table: "threads",
                column: "content")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_threads_title_trgm",
                table: "threads",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_votes_reply_id",
                table: "votes",
                column: "reply_id");

            migrationBuilder.CreateIndex(
                name: "ix_votes_thread_id",
                table: "votes",
                column: "thread_id");

            migrationBuilder.CreateIndex(
                name: "ux_votes_user_reply",
                table: "votes",
                columns: new[] { "user_id", "reply_id" },
                unique: true,
                filter: "reply_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_votes_user_thread",
                table: "votes",
                columns: new[] { "user_id", "thread_id" },
                unique: true,
                filter: "thread_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "votes");

            migrationBuilder.DropTable(
                name: "replies");

            migrationBuilder.DropTable(
                name: "threads");

            migrationBuilder.DropTable(
                name: "boards");
        }
    }
}
