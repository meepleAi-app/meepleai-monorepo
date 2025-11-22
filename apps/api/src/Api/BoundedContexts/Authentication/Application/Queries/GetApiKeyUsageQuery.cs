using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get usage statistics for an API key.
/// </summary>
public record GetApiKeyUsageQuery(
    string KeyId,
    string UserId
) : IQuery<ApiKeyQuotaDto?>;
