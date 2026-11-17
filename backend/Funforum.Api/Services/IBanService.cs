using Funforum.Api.Common;
using Funforum.Api.Dtos.Admin;

namespace Funforum.Api.Services;

// X.4 — ban/unban tài khoản + IP, và kiểm tra trạng thái ban khi đăng ký/đăng bài.
public interface IBanService
{
    Task<PagedResult<BanDto>> ListAsync(
        int page, int pageSize, string? status, int? userId,
        string? sort, string? order, CancellationToken ct);

    Task<BanDto> CreateAsync(CreateBanRequest request, int actingAdminId, CancellationToken ct);

    Task<BanDto> UnbanAsync(long banId, int actingAdminId, CancellationToken ct);

    // User bị ban (theo user_id) HOẶC IP hiện tại trùng banned_ip active (bắt tài khoản phụ).
    Task<bool> IsBlockedAsync(int userId, string? ip, CancellationToken ct);

    Task<bool> IsIpBannedAsync(string ip, CancellationToken ct);
}
