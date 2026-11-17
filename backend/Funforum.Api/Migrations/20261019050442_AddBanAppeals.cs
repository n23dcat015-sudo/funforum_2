using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Funforum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBanAppeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ban_appeals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ban_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    reviewed_by_admin_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_appeals", x => x.id);
                    table.CheckConstraint("ck_ban_appeals_reason_length", "char_length(reason_text) BETWEEN 1 AND 1000");
                    table.CheckConstraint("ck_ban_appeals_status", "status IN ('pending','rejected','approved')");
                    table.ForeignKey(
                        name: "fk_ban_appeals_admins_reviewed_by_admin_id",
                        column: x => x.reviewed_by_admin_id,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ban_appeals_bans_ban_id",
                        column: x => x.ban_id,
                        principalTable: "bans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ban_appeals_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ban_appeals_ban_id",
                table: "ban_appeals",
                column: "ban_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_appeals_reviewed_by_admin_id",
                table: "ban_appeals",
                column: "reviewed_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_appeals_status_created_at",
                table: "ban_appeals",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ban_appeals_user_id",
                table: "ban_appeals",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ban_appeals");
        }
    }
}
