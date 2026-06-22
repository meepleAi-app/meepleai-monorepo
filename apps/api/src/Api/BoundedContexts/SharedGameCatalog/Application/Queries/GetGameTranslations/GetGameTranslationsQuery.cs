using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslations;

/// <summary>
/// Admin query returning every active translation for a single shared game.
/// Issue #2339 — sub-PR 1/3 Wave 4 (Task 12).
/// </summary>
/// <param name="GameId">Owning <c>shared_games.id</c>.</param>
public sealed record GetGameTranslationsQuery(Guid GameId)
    : IRequest<IReadOnlyList<SharedGameTranslationDetailDto>>;
