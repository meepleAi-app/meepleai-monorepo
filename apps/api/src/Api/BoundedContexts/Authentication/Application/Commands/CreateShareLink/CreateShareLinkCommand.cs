using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;

/// <summary>
/// Command to create a shareable link for a chat thread.
/// </summary>
/// <param name="ThreadId">Chat thread to share</param>
/// <param name="Role">Access level (view or comment)</param>
/// <param name="ExpiresAt">Expiration timestamp (UTC)</param>
/// <param name="Label">Optional label for the share link</param>
/// <param name="UserId">User creating the share link (from auth context)</param>
public sealed record CreateShareLinkCommand(
    Guid ThreadId,
    ShareLinkRole Role,
    DateTime ExpiresAt,
    string? Label,
    Guid UserId
) : IRequest<CreateShareLinkResult>;

/// <summary>
/// Result of share link creation operation.
/// </summary>
/// <param name="ShareLinkId">Unique identifier for the created share link</param>
/// <param name="Token">JWT token to embed in shareable URL</param>
/// <param name="ExpiresAt">Expiration timestamp</param>
/// <param name="ShareableUrl">Complete URL with embedded token</param>
public sealed record CreateShareLinkResult(
    Guid ShareLinkId,
    string Token,
    DateTime ExpiresAt,
    string ShareableUrl
);
