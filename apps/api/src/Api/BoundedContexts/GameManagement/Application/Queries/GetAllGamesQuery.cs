using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all games in the catalog.
/// </summary>
public record GetAllGamesQuery() : IQuery<IReadOnlyList<GameDto>>;
