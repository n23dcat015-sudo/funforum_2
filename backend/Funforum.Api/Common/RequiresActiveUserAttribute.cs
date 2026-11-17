namespace Funforum.Api.Common;

// X.4 — đánh dấu action chỉ dành cho user đang ở trạng thái active. BanCheckMiddleware
// đọc metadata này để chặn POST/đăng bài/vote khi tài khoản bị ban.
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresActiveUserAttribute : Attribute
{
}
