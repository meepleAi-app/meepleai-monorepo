using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve a single game by ID.
/// </summary>
internal record GetGameByIdQuery(
    Guid GameId
) : IQuery<GameDto?>;
