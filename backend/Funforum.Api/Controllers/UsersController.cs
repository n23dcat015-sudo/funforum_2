using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Auth;
using Funforum.Api.Entities;
using Funforum.Api.Security;

namespace Funforum.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthPolicies.UserOnly)]
public class UsersController(FunforumDbContext db, IPasswordHasher hasher) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken ct)
    {
        var user = await db.Users.FindAsync([GetUserId()], ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");

        return Ok(ToMeResponse(user));
    }

    [HttpPut("me")]
    public async Task<ActionResult<MeResponse>> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.BirthYear is short year && (year < 1900 || year > DateTime.UtcNow.Year))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Birth year is out of range.");
        }

        var user = await db.Users.FindAsync([GetUserId()], ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");

        // X.5 — user đang bị ban không sửa được thông tin cá nhân (chỉ được kháng cáo).
        if (UserStatuses.IsBanned(user.Status))
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "Your account is banned. You cannot edit your profile until the ban is lifted.");
        }

        // Phase A: tên hiển thị công khai; gửi lên thì phải là chuỗi 1..100 ký tự.
        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            if (displayName.Length is < 1 or > 100)
            {
                throw new ApiException(StatusCodes.Status400BadRequest, "Display name must be 1-100 characters.");
            }
            user.DisplayName = displayName;
        }

        user.FullName = request.FullName;
        user.BirthYear = request.BirthYear;
        user.Address = request.Address;
        user.Phone = request.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(ToMeResponse(user));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");

        if (user.PasswordHash is null || !hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Current password is incorrect.");
        }

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Đổi mật khẩu thì thu hồi mọi refresh token cũ — current_state.md mục 5.
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private int GetUserId() => int.Parse(User.FindFirst("sub")!.Value);

    private static MeResponse ToMeResponse(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        FullName = user.FullName,
        BirthYear = user.BirthYear,
        Address = user.Address,
        Phone = user.Phone,
        Status = user.Status,
        CreatedAt = user.CreatedAt,
    };
}
