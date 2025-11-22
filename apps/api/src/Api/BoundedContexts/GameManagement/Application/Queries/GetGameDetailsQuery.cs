using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve comprehensive game details including statistics.
/// </summary>
public record GetGameDetailsQuery(
    Guid GameId
) : IQuery<GameDetailsDto?>;
