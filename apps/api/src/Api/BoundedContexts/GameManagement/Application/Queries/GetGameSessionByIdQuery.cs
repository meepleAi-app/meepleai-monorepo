using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve a game session by ID.
/// </summary>
public record GetGameSessionByIdQuery(
    Guid SessionId
) : IQuery<GameSessionDto?>;
