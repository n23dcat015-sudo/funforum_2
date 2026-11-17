using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funforum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlterAdminsAddRole : Migration
    {
        // X.1: role 3 cấp (janitor/moderator/super_admin) thay cho is_super_admin,
        // capcode mở rộng tương ứng. Không thể đảo ngược hoàn toàn: capcode cũ
        // ('admin'/'superadmin') đã bị ánh xạ lại nên Down() chỉ khôi phục gần đúng.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Thêm cột role với DEFAULT để không phải rewrite bảng; giá trị mặc định
            //    'moderator' khớp với ánh xạ is_super_admin=false -> moderator.
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "admins",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "moderator");

            // 2) Backfill super_admin TRƯỚC khi drop is_super_admin.
            migrationBuilder.Sql("UPDATE admins SET role = 'super_admin' WHERE is_super_admin;");

            // 3) Bỏ default — role phải được set tường minh từ code.
            migrationBuilder.Sql("ALTER TABLE admins ALTER COLUMN role DROP DEFAULT;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admins_role",
                table: "admins",
                sql: "role IN ('janitor','moderator','super_admin')");

            // 4) Capcode: phải drop CHECK cũ trước (giá trị mới không hợp lệ với nó),
            //    backfill, rồi mới add CHECK mới — ADD CONSTRAINT validate toàn bộ row
            //    hiện có nên thứ tự sai sẽ lỗi 23514.
            migrationBuilder.DropCheckConstraint(
                name: "ck_threads_capcode",
                table: "threads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_replies_capcode",
                table: "replies");

            // 'superadmin' -> 'super_admin'; 'admin' -> 'moderator' vì backfill admins
            // ánh xạ is_super_admin=false thành moderator, nên mọi tác giả từng post
            // dưới capcode 'admin' nay là moderator.
            migrationBuilder.Sql("UPDATE threads SET capcode = 'super_admin' WHERE capcode = 'superadmin';");
            migrationBuilder.Sql("UPDATE threads SET capcode = 'moderator' WHERE capcode = 'admin';");
            migrationBuilder.Sql("UPDATE replies SET capcode = 'super_admin' WHERE capcode = 'superadmin';");
            migrationBuilder.Sql("UPDATE replies SET capcode = 'moderator' WHERE capcode = 'admin';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_threads_capcode",
                table: "threads",
                sql: "capcode IS NULL OR capcode IN ('janitor','moderator','super_admin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_replies_capcode",
                table: "replies",
                sql: "capcode IS NULL OR capcode IN ('janitor','moderator','super_admin')");

            // 5) Drop cột cũ cuối cùng.
            migrationBuilder.DropColumn(
                name: "is_super_admin",
                table: "admins");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_admins_role",
                table: "admins");

            migrationBuilder.AddColumn<bool>(
                name: "is_super_admin",
                table: "admins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE admins SET is_super_admin = (role = 'super_admin');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_threads_capcode",
                table: "threads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_replies_capcode",
                table: "replies");

            // janitor không có tương ứng ở schema cũ -> 'admin' (bậc không phải super).
            migrationBuilder.Sql("UPDATE threads SET capcode = 'superadmin' WHERE capcode = 'super_admin';");
            migrationBuilder.Sql("UPDATE threads SET capcode = 'admin' WHERE capcode IN ('moderator','janitor');");
            migrationBuilder.Sql("UPDATE replies SET capcode = 'superadmin' WHERE capcode = 'super_admin';");
            migrationBuilder.Sql("UPDATE replies SET capcode = 'admin' WHERE capcode IN ('moderator','janitor');");

            migrationBuilder.AddCheckConstraint(
                name: "ck_threads_capcode",
                table: "threads",
                sql: "capcode IS NULL OR capcode IN ('admin','superadmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_replies_capcode",
                table: "replies",
                sql: "capcode IS NULL OR capcode IN ('admin','superadmin')");

            migrationBuilder.DropColumn(
                name: "role",
                table: "admins");
        }
    }
}
