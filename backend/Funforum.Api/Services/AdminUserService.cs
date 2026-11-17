using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;

namespace Funforum.Api.Services;

public sealed class AdminUserService(
    FunforumDbContext db,
    IAuditLogger audit,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public async Task<PagedResult<AdminUserDto>> ListAsync(
        int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Username.Contains(term) || u.Email.Contains(term) || (u.FullName != null && u.FullName.Contains(term)));
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id),
            "username" => descending ? query.OrderByDescending(u => u.Username).ThenByDescending(u => u.Id) : query.OrderBy(u => u.Username).ThenBy(u => u.Id),
            "email" => descending ? query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id) : query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            "fullname" => descending ? query.OrderByDescending(u => u.FullName).ThenByDescending(u => u.Id) : query.OrderBy(u => u.FullName).ThenBy(u => u.Id),
            "birthyear" => descending ? query.OrderByDescending(u => u.BirthYear).ThenByDescending(u => u.Id) : query.OrderBy(u => u.BirthYear).ThenBy(u => u.Id),
            "createdat" => descending ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id) : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            _ => descending ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<AdminUserDto>
        {
            Items = users.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AdminUserDto> GetAsync(int id, CancellationToken ct) =>
        ToDto(await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new ApiException(404, "User not found."));

    public async Task<AdminUserDto> UpdateAsync(int id, UpdateAdminUserRequest request, int adminId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new ApiException(404, "User not found.");

        if (request.BirthYear is short year && (year < 1900 || year > DateTime.UtcNow.Year))
        {
            throw new ApiException(400, "Birth year is out of range.");
        }

        if (request.FullName is not null && string.IsNullOrWhiteSpace(request.FullName.Trim()))
        {
            throw new ApiException(400, "Full name cannot be empty.");
        }

        if (request.Address is not null && string.IsNullOrWhiteSpace(request.Address.Trim()))
        {
            throw new ApiException(400, "Address cannot be empty.");
        }

        if (request.Phone is not null && string.IsNullOrWhiteSpace(request.Phone.Trim()))
        {
            throw new ApiException(400, "Phone cannot be empty.");
        }

        user.FullName = request.FullName is null ? null : request.FullName.Trim();
        user.BirthYear = request.BirthYear;
        user.Address = request.Address is null ? null : request.Address.Trim();
        user.Phone = request.Phone is null ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "update", "user", id);
        return ToDto(user);
    }

    // X.5 — khóa quyền kháng cáo: đặt status = banned_permanently (không còn form nộp đơn).
    public async Task<AdminUserDto> LockAppealAsync(int id, int adminId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new ApiException(404, "User not found.");
        if (user.Status == UserStatuses.BannedPermanently)
        {
            throw new ApiException(409, "Appeal privilege is already locked for this user.");
        }

        var previousStatus = user.Status;
        user.Status = UserStatuses.BannedPermanently;
        user.UpdatedAt = DateTime.UtcNow;

        audit.Write(
            adminId, AuditActions.Ban, AuditTargetTypes.User, id,
            new { status = previousStatus, lockAppeal = true }, "Appeal privilege locked");

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} locked appeal privilege for {EntityType} {EntityId}", adminId, "user", id);
        return ToDto(user);
    }

    public async Task DeleteAsync(int id, int adminId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new ApiException(404, "User not found.");

        // Metadata đủ để X.7 khôi phục hồ sơ (không gồm password_hash).
        audit.Write(adminId, AuditActions.Delete, AuditTargetTypes.User, id, metadata: new
        {
            user.Username,
            user.Email,
            user.FullName,
            user.BirthYear,
            user.Address,
            user.Phone,
            user.CreatedAt,
        });

        await db.Threads
            .Where(t => t.AuthorUserId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AuthorUserId, (int?)null), ct);

        await db.Replies
            .Where(r => r.AuthorUserId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.AuthorUserId, (int?)null), ct);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", adminId, "delete", "user", id);
    }

    private static AdminUserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        BirthYear = user.BirthYear,
        Address = user.Address,
        Phone = user.Phone,
        Status = user.Status,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
    };
}
