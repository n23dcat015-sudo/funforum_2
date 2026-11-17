using Funforum.Api.Dtos.Auth;

namespace Funforum.Api.Services;

public interface IAuthService
{
    Task<int> RegisterUserAsync(RegisterRequest request, string? ip, CancellationToken ct);

    Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginUserAsync(
        LoginRequest request, CancellationToken ct);

    Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginAdminAsync(
        AdminLoginRequest request, CancellationToken ct);

    Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> LoginWithGoogleAsync(
        GoogleLoginRequest request, CancellationToken ct);

    Task<(AuthResponse Auth, string RawRefreshToken, DateTime RefreshExpiresAt)> RefreshAsync(
        string rawRefreshToken, CancellationToken ct);

    Task LogoutAsync(string rawRefreshToken, CancellationToken ct);
}
