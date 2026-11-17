using Microsoft.AspNetCore.Authorization;

namespace Funforum.Api.Security;

// X.1: một requirement phân cấp thay cho 3 policy rời rạc — các policy chỉ khác nhau
// ở role tối thiểu, nên thêm cấp mới không phải thêm policy mới.
public sealed record RoleRequirement(string MinimumRole) : IAuthorizationRequirement;
