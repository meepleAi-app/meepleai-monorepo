using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get all API keys with usage statistics (admin only).
/// </summary>
public record GetAllApiKeysWithStatsQuery(
    Guid? UserId = null, // Filter by user ID (optional)
    bool IncludeRevoked = false
) : IQuery<List<ApiKeyWithStatsDto>>;
