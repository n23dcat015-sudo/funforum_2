namespace Funforum.Api.Security;

// Phase D — thông tin lấy được từ ID token Google đã verify.
public record GoogleUserInfo(string Sub, string Email, bool EmailVerified, string? Name);

public interface IGoogleTokenValidator
{
    // Ném ApiException(401) nếu token sai; ApiException(503) nếu chưa cấu hình ClientId.
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct);
}
