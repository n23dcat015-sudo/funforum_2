namespace Funforum.Api.Common;

// current_state.md mục 4.3 / mục 11.8: EF.Functions.ILike không tự escape wildcard,
// phải escape thủ công rồi truyền escape character vào tham số thứ 3 của ILike.
public static class LikePatternEscaper
{
    public const char EscapeCharacter = '\\';

    public static string Escape(string term) =>
        term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}