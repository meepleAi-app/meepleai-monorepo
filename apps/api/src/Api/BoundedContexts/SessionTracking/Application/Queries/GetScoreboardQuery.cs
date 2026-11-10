using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetScoreboardQuery(
    Guid SessionId,
    Guid RequestedBy // #4883: owner/participant-only; derived from the principal, never from the client.
) : IRequest<ScoreboardDto>;
