namespace Funforum.Api.Common;

// X.2 — từ vựng cố định của audit log. CHECK constraint trong DB phải khớp đúng
// danh sách này; thêm giá trị mới nghĩa là thêm migration.
public static class AuditActions
{
    public const string Hide = "hide";
    public const string Unhide = "unhide";
    public const string Delete = "delete";
    public const string Ban = "ban";
    public const string Unban = "unban";
    public const string RoleChange = "role_change";
    public const string BoardCreate = "board_create";
    public const string BoardDelete = "board_delete";
    public const string WordfilterEdit = "wordfilter_edit";
}

public static class AuditTargetTypes
{
    public const string Thread = "thread";
    public const string Reply = "reply";
    public const string Board = "board";
    public const string Admin = "admin";
    public const string User = "user";
}
