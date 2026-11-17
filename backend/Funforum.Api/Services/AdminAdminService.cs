using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Dtos.Admin;
using Funforum.Api.Entities;
using Funforum.Api.Security;

namespace Funforum.Api.Services;

public sealed class AdminAdminService(
    FunforumDbContext db,
    IPasswordHasher hasher,
    IAuditLogger audit,
    ILogger<AdminAdminService> logger) : IAdminAdminService
{
    public async Task<PagedResult<AdminAccountDto>> ListAsync(
        int page, int pageSize, string? q, string? sort, string? order, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, ContentLimits.MaxPageSize);

        var query = db.Admins.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.Username.Contains(term) || a.Email.Contains(term));
        }

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.ToLowerInvariant()) switch
        {
            "id" => descending ? query.OrderByDescending(a => a.Id) : query.OrderBy(a => a.Id),
            "username" => descending ? query.OrderByDescending(a => a.Username).ThenByDescending(a => a.Id) : query.OrderBy(a => a.Username).ThenBy(a => a.Id),
            "email" => descending ? query.OrderByDescending(a => a.Email).ThenByDescending(a => a.Id) : query.OrderBy(a => a.Email).ThenBy(a => a.Id),
            "role" => descending ? query.OrderByDescending(a => a.Role).ThenByDescending(a => a.Id) : query.OrderBy(a => a.Role).ThenBy(a => a.Id),
            "createdat" => descending ? query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id) : query.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id),
            _ => descending ? query.OrderByDescending(a => a.Id) : query.OrderBy(a => a.Id),
        };

        var totalItems = await query.CountAsync(ct);
        var admins = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<AdminAccountDto>
        {
            Items = admins.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AdminAccountDto> GetAsync(int id, CancellationToken ct) =>
        ToDto(await db.Admins.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ApiException(404, "Admin not found."));

    public async Task<AdminAccountDto> CreateAsync(CreateAdminAccountRequest request, int actingAdminId, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            throw new ApiException(400, "Username and email are required.");
        }

        if (ReservedUsernames.IsReserved(username))
        {
            throw new ApiException(400, "This username is not allowed.");
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ApiException(400, "Email format is invalid.");
        }

        var role = request.Role.Trim();
        if (!Roles.IsAdmin(role))
        {
            throw new ApiException(400, "Role must be janitor, moderator or super_admin.");
        }

        var acting = await RequireActingAdminAsync(actingAdminId, ct);

        if (await db.Admins.AnyAsync(a => a.Username == username || a.Email == email, ct))
        {
            throw new ApiException(409, "Admin username or email already exists.");
        }

        var admin = new Admin
        {
            Username = username,
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Admins.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", actingAdminId, "create", "admin", admin.Id);
        return ToDto(admin);
    }

    public async Task<AdminAccountDto> UpdateAsync(int id, UpdateAdminAccountRequest request, int actingAdminId, CancellationToken ct)
    {
        _ = await RequireActingAdminAsync(actingAdminId, ct);

        var admin = await db.Admins.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ApiException(404, "Admin not found.");

        var candidateUsername = request.Username is null ? admin.Username : request.Username.Trim();
        if (string.IsNullOrWhiteSpace(candidateUsername))
        {
            throw new ApiException(400, "Username cannot be empty.");
        }

        if (ReservedUsernames.IsReserved(candidateUsername))
        {
            throw new ApiException(400, "This username is not allowed.");
        }

        var candidateEmail = request.Email is null ? admin.Email : request.Email.Trim();
        if (string.IsNullOrWhiteSpace(candidateEmail) || !new EmailAddressAttribute().IsValid(candidateEmail))
        {
            throw new ApiException(400, "Email format is invalid.");
        }

        if ((request.Username is not null || request.Email is not null) &&
            await db.Admins.AnyAsync(a => a.Id != id && (a.Username == candidateUsername || a.Email == candidateEmail), ct))
        {
            throw new ApiException(409, "Admin username or email already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            admin.Username = candidateUsername;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            admin.Email = candidateEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            admin.PasswordHash = hasher.Hash(request.Password);
        }

        admin.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", actingAdminId, "update", "admin", id);
        return ToDto(admin);
    }

    public async Task<AdminAccountDto> ChangeRoleAsync(int id, UpdateAdminRoleRequest request, int actingAdminId, CancellationToken ct)
    {
        var acting = await RequireActingAdminAsync(actingAdminId, ct);

        var admin = await db.Admins.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ApiException(404, "Admin not found.");

        var newRole = request.Role.Trim();
        if (!Roles.IsAdmin(newRole))
        {
            throw new ApiException(400, "Role must be janitor, moderator or super_admin.");
        }

        var superAdminCount = await db.Admins.CountAsync(a => a.Role == Roles.SuperAdmin, ct);
        if (!AdminSecurityGuard.CanChangeRole(admin.Role, acting.Role, newRole, superAdminCount))
        {
            throw new ApiException(403, "This role change is not allowed.");
        }

        var previousRole = admin.Role;
        admin.Role = newRole;
        admin.UpdatedAt = DateTime.UtcNow;
        audit.Write(actingAdminId, AuditActions.RoleChange, AuditTargetTypes.Admin, id,
            metadata: new { role = previousRole });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", actingAdminId, "role_change", "admin", id);
        return ToDto(admin);
    }

    public async Task DeleteAsync(int id, int actingAdminId, CancellationToken ct)
    {
        var acting = await RequireActingAdminAsync(actingAdminId, ct);

        var admin = await db.Admins.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ApiException(404, "Admin not found.");

        var superAdminCount = await db.Admins.CountAsync(a => a.Role == Roles.SuperAdmin, ct);
        if (!AdminSecurityGuard.CanDeleteAdmin(id, actingAdminId, admin.Role, acting.Role, superAdminCount))
        {
            throw new ApiException(403, "This admin cannot be deleted.");
        }

        // Metadata đủ để X.7 khôi phục tài khoản admin (không gồm password_hash).
        audit.Write(actingAdminId, AuditActions.Delete, AuditTargetTypes.Admin, id, metadata: new
        {
            admin.Username,
            admin.Email,
            admin.Role,
            admin.CreatedAt,
        });

        await db.Threads
            .Where(t => t.AuthorAdminId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AuthorAdminId, (int?)null), ct);

        await db.Replies
            .Where(r => r.AuthorAdminId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.AuthorAdminId, (int?)null), ct);

        db.Admins.Remove(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin {AdminId} performed {Action} on {EntityType} {EntityId}", actingAdminId, "delete", "admin", id);
    }

    // is_super_admin/role luôn đọc từ DB, không tin claim trong token (mục 11).
    private async Task<Admin> RequireActingAdminAsync(int actingAdminId, CancellationToken ct) =>
        await db.Admins.AsNoTracking().SingleOrDefaultAsync(a => a.Id == actingAdminId, ct)
            ?? throw new ApiException(403, "Acting admin not found.");

    private static AdminAccountDto ToDto(Admin admin) => new()
    {
        Id = admin.Id,
        Username = admin.Username,
        Email = admin.Email,
        Role = admin.Role,
        CreatedAt = admin.CreatedAt,
        UpdatedAt = admin.UpdatedAt,
    };
}
