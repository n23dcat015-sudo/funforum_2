using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Funforum.Api.Common;
using Funforum.Api.Dtos.Auth;
using Funforum.Api.Services;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IPasswordResetService passwordResetService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var id = await authService.RegisterUserAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var (auth, rawRefresh, refreshExpiresAt) = await authService.LoginUserAsync(request, ct);
        SetRefreshCookie(rawRefresh, refreshExpiresAt);
        return Ok(auth);
    }

    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> AdminLogin([FromBody] AdminLoginRequest request, CancellationToken ct)
    {
        var (auth, rawRefresh, refreshExpiresAt) = await authService.LoginAdminAsync(request, ct);
        SetRefreshCookie(rawRefresh, refreshExpiresAt);
        return Ok(auth);
    }

    // Phase D — đăng nhập/đăng ký bằng Google ID token.
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var (auth, rawRefresh, refreshExpiresAt) = await authService.LoginWithGoogleAsync(request, ct);
        SetRefreshCookie(rawRefresh, refreshExpiresAt);
        return Ok(auth);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawRefresh))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "No refresh token cookie.");
        }

        var (auth, newRawRefresh, refreshExpiresAt) = await authService.RefreshAsync(rawRefresh, ct);
        SetRefreshCookie(newRawRefresh, refreshExpiresAt);
        return Ok(auth);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawRefresh))
        {
            await authService.LogoutAsync(rawRefresh, ct);
        }

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    // Phase C — luôn 204 dù email có tồn tại hay không (chống enumeration); rate-limit thô
    // theo IP để đỡ spam.
    [HttpPost("forgot-password")]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await passwordResetService.RequestResetAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await passwordResetService.ResetAsync(request.Token, request.NewPassword, ct);
        return NoContent();
    }

    // Refresh token: HttpOnly + SameSite=Strict + Path giới hạn ở /api/auth, Secure bắt
    // buộc khi không chạy Development (current_state.md mục 5 và mục 11 / roadmap GĐ9).
    private void SetRefreshCookie(string rawToken, DateTime expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = expiresAt,
        });
    }
}
