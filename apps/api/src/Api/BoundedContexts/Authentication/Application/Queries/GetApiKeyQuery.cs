using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get a specific API key by ID.
/// </summary>
public record GetApiKeyQuery(
    string KeyId,
    string UserId
) : IQuery<ApiKeyDto?>;
