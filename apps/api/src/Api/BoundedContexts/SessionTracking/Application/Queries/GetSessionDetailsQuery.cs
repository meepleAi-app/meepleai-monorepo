using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionDetailsQuery(
    Guid SessionId
) : IRequest<SessionDetailsDto>;
