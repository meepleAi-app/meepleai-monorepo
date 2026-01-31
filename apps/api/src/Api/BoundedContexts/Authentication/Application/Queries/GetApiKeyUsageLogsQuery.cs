using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get usage logs for a specific API key with pagination.
/// </summary>
internal record GetApiKeyUsageLogsQuery(
    Guid KeyId,
    Guid UserId,
    int Skip = 0,
    int Take = 100
) : IQuery<List<ApiKeyUsageLogDto>>;
