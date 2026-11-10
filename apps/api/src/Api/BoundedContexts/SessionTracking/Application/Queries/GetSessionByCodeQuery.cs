using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// The session behind a join code, for the caller who may read it.
/// </summary>
/// <param name="SessionCode">The 6-character join code.</param>
/// <param name="RequestedBy">
/// The authenticated caller, derived from the principal (#4776). #4939: the code is a credential (#4883), but
/// knowing it is not being at the table: the full <see cref="SessionDto"/> (owner, participants' user ids,
/// scores) is answered only to the owner or a participant; anyone else gets the same <c>null</c> as an
/// unknown code, so that a guessed code does not even confirm it is live.
/// </param>
public record GetSessionByCodeQuery(
    string SessionCode,
    Guid RequestedBy
) : IRequest<SessionDto?>;
