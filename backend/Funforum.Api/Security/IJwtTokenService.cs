namespace Funforum.Api.Security;

public interface IJwtTokenService
{
    /// <summary>
    /// Tạo access token cho user/admin theo role và subjectId tương ứng.
    /// </summary>
    /// <param name="subjectId">users.id hoặc admins.id (2 dãy số độc lập).</param>
    /// <param name="role">"user" | "admin" | "superadmin".</param>
    (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(int subjectId, string role);
}
