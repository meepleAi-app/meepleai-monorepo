using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionHistoryQuery(
    Guid UserId,
    Guid? GameId,
    int Limit,
    int Offset
) : IRequest<List<SessionSummaryDto>>;
