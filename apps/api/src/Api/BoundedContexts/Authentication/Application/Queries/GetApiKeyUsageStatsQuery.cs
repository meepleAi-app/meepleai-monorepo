using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get detailed usage statistics for a specific API key.
/// </summary>
public record GetApiKeyUsageStatsQuery(
    Guid KeyId,
    Guid UserId
) : IQuery<ApiKeyUsageStatsDto?>;
