using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Admin;

// Đổi role đi qua PATCH /admin/admins/{id}/role riêng để mọi lần đổi cấp đều qua
// AdminSecurityGuard.CanChangeRole và ghi audit log action "role_change".
public sealed class UpdateAdminAccountRequest
{
    [MinLength(3), MaxLength(32)]
    public string? Username { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    [MinLength(8), MaxLength(64)]
    public string? Password { get; init; }
}
