using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Funforum.Api.Common;

namespace Funforum.Api.Security;

// Phase D — verify ID token của Google: chữ ký, iss, exp do thư viện kiểm; ta ép thêm
// aud == Google:ClientId (auth_google_email_plan.md §3 Phase D).
public class GoogleTokenValidator(IOptions<GoogleOptions> options) : IGoogleTokenValidator
{
    private readonly GoogleOptions _options = options.Value;

    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Google Sign-In is not configured.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId],
            });

            return new GoogleUserInfo(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Invalid Google token.");
        }
    }
}
