namespace Funforum.Api.Common;

// current_state.md mục 3.6. Giới hạn mỗi-thread (mục 3.8: số reply, số ảnh) không còn ở
// đây — xem ContentLimitsOptions, đọc được từ biến môi trường.
public static class ContentLimits
{
    public const int ThreadTitleMax = 200;
    public const int ThreadContentMax = 10_000;
    public const int ReplyContentMax = 5_000;

    public const int BoardNameMax = 100;
    public const int BoardDescriptionMax = 500;

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int BoardIndexPageSize = 10;
    public const int RootRepliesPageSize = 20;

    public const int RootReplyPreviewCount = 10;
    public const int ChildrenPageSize = 10;

    // X.3 / X.4 — độ dài lý do report và ban (khớp CHECK/độ dài cột trong DB).
    public const int ReportReasonMax = 500;
    public const int BanReasonMax = 500;

    // X.5 — độ dài lý do kháng cáo (khớp ban_appeals.reason_text trong DB).
    public const int AppealReasonMax = 1000;
}