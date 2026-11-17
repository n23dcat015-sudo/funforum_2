using Funforum.Api.Common;
using Xunit;

namespace Funforum.Api.Tests.Common;

// current_state.md mục 4.3: test escape với %, _, \, %%, _a% phải lọc kết quả chính xác.
public class LikePatternEscaperTests
{
    [Theory]
    [InlineData("%", "\\%")]
    [InlineData("_", "\\_")]
    [InlineData("\\", "\\\\")]
    [InlineData("%%", "\\%\\%")]
    [InlineData("_a%", "\\_a\\%")]
    [InlineData("hello", "hello")]
    public void Escape_HandlesWildcardsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, LikePatternEscaper.Escape(input));
    }

    [Fact]
    public void Escape_BackslashHandledBeforeOtherCharacters()
    {
        Assert.Equal("\\\\\\%", LikePatternEscaper.Escape("\\%"));
    }
}
