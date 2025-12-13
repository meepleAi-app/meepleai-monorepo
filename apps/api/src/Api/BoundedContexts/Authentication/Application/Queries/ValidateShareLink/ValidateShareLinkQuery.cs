using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;

/// <summary>
/// Query to validate a share link JWT token.
/// </summary>
/// <param name="TokenValue">JWT token string from URL query parameter</param>
public sealed record ValidateShareLinkQuery(string TokenValue) : IRequest<ValidateShareLinkResult?>;

/// <summary>
/// Result of share link validation.
/// </summary>
/// <param name="ShareLinkId">Unique identifier for the share link</param>
/// <param name="ThreadId">Chat thread identifier</param>
/// <param name="Role">Access level granted</param>
/// <param name="CreatorId">User who created the share link</param>
/// <param name="ExpiresAt">Expiration timestamp</param>
/// <param name="IsValid">True if token is valid and not revoked/expired</param>
public sealed record ValidateShareLinkResult(
    Guid ShareLinkId,
    Guid ThreadId,
    ShareLinkRole Role,
    Guid CreatorId,
    DateTime ExpiresAt,
    bool IsValid
);
