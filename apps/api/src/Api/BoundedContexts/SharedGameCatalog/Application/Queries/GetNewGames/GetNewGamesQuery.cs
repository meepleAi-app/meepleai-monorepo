using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Query to retrieve the most recently added shared games.
/// Used by the Discover dashboard "New Games" widget.
/// Issue #728.
/// </summary>
internal sealed record GetNewGamesQuery(int Limit) : IQuery<IReadOnlyList<NewGameDto>>;
