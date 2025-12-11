using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Authentication.Application.Queries.OAuth;

/// <summary>
/// Query to retrieve all OAuth accounts linked to a user.
/// Returns list of linked accounts with provider and status information.
/// </summary>
public sealed record GetLinkedOAuthAccountsQuery : IQuery<GetLinkedOAuthAccountsResult>
{
    public Guid UserId { get; init; }
}

/// <summary>
/// Result containing all linked OAuth accounts for a user.
/// </summary>
public sealed record GetLinkedOAuthAccountsResult
{
    public List<OAuthAccountDto> Accounts { get; init; } = new();
}

/// <summary>
/// DTO representing an OAuth account linked to a user.
/// </summary>
public sealed record OAuthAccountDto
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderUserId { get; init; } = string.Empty;
    public bool IsTokenExpired { get; init; }
    public bool SupportsRefresh { get; init; }
    public DateTime CreatedAt { get; init; }
}
