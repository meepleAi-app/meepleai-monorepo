using MediatR;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionByCodeQuery(
    string SessionCode
) : IRequest<SessionDto?>;
