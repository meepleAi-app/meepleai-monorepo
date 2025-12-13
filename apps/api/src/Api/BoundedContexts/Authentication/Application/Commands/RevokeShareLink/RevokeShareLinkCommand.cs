using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;

/// <summary>
/// Command to revoke a shareable link, making it immediately invalid.
/// </summary>
/// <param name="ShareLinkId">Share link to revoke</param>
/// <param name="UserId">User requesting revocation (must be creator or admin)</param>
public sealed record RevokeShareLinkCommand(
    Guid ShareLinkId,
    Guid UserId
) : IRequest<bool>;
