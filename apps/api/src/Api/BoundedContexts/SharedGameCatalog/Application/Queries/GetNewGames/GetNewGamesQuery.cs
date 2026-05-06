using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Query for the most recently created shared games in the catalog
/// (Wave 3 Phase 1, PR #732 §4.3.2 / Issue #805).
/// </summary>
/// <param name="Limit">Number of games to return. Validator clamps to [1, 50]; default 10.</param>
internal sealed record GetNewGamesQuery(int Limit = 10) : IQuery<IReadOnlyList<NewGameDto>>;
