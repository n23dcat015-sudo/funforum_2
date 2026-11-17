using System.Security.Claims;
using Funforum.Api.Dtos.Threads;

namespace Funforum.Api.Services;

public interface IThreadService
{
    Task<ThreadDto> CreateAsync(
        string boardSlug, CreateThreadRequest request, ClaimsPrincipal user, string? ip, CancellationToken ct);

    Task<ThreadDto> GetByIdAsync(int id, ClaimsPrincipal user, CancellationToken ct);

    Task<ThreadDto> UpdateAsync(int id, UpdateThreadRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task DeleteAsync(int id, ClaimsPrincipal user, CancellationToken ct);
}