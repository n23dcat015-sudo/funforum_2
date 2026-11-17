namespace Funforum.Api.Common;

// current_state.md mục 3.8: "cấu hình được bằng biến môi trường". ASP.NET Core tự ánh xạ
// biến môi trường ContentLimits__MaxRepliesPerThread / ContentLimits__MaxImagesPerThread
// (dấu "__" = cấp con) vào đúng 2 property này, đè lên giá trị mặc định ở appsettings.json.
public class ContentLimitsOptions
{
    public const string SectionName = "ContentLimits";

    public int MaxRepliesPerThread { get; set; } = 500;
    public int MaxImagesPerThread { get; set; } = 150;
}