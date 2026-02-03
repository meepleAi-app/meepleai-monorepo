using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetScoreboardQuery(
    Guid SessionId
) : IRequest<ScoreboardDto>;
