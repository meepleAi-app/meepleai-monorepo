using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session details for a caller: answered only to the owner or a participant (#4883), because the
/// details carry the session code, who sits at the table, the scores and the shared notes.
/// </summary>
/// <param name="SessionId">Session to read.</param>
/// <param name="RequestedBy">Authenticated caller, derived from the principal — never from the client.</param>
public record GetSessionDetailsQuery(
    Guid SessionId,
    Guid RequestedBy
) : IRequest<SessionDetailsDto>;

/// <summary>
/// Session details for the server itself, with no caller to authorize. For other bounded contexts that
/// read the companion session on behalf of a flow they have already authorized themselves (the
/// game-night bridge: the organizer going live need not sit in the tracking session). Never send this
/// from an endpoint — an HTTP read goes through <see cref="GetSessionDetailsQuery"/>.
/// </summary>
public record GetSessionDetailsForSystemQuery(
    Guid SessionId
) : IRequest<SessionDetailsDto>;
