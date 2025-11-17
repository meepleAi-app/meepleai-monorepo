using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to list all API keys for a specific user with pagination.
/// </summary>
public record ListApiKeysQuery(
    string UserId,
    bool IncludeRevoked = false,
    int Page = 1,
    int PageSize = 20
) : IQuery<ApiKeyListResponse>;
