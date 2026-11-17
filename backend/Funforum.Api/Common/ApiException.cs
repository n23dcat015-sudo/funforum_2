namespace Funforum.Api.Common;

// Service ném exception này khi gặp lỗi nghiệp vụ (409 trùng dữ liệu, 401 sai thông
// tin đăng nhập, 400 vi phạm quy tắc...); Controller bắt và trả ProblemDetails
// đúng status code (mục "Quy ước chung" — Phụ lục A).
public class ApiException : Exception
{
    public ApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public ApiException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
