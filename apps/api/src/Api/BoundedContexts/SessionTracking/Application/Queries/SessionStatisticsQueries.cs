using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionStatisticsQuery(Guid UserId, int MonthsBack = 6) : IRequest<SessionStatisticsDto>;

public record GetGameStatisticsQuery(Guid UserId, Guid GameId) : IRequest<GameStatisticsDto>;
