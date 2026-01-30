using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetActiveSessionQuery(
    Guid UserId
) : IRequest<SessionDto?>;
