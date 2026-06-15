using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Default <see cref="IGameTitleResolver"/> backed by
/// <see cref="ISharedGameTranslationRepository.GetByGameIdsAsync"/>.
/// Issue #2339 — sub-PR 1/3 Wave 3 (Task 8).
/// </summary>
internal sealed class GameTitleResolver : IGameTitleResolver
{
    private readonly ISharedGameTranslationRepository _repo;

    public GameTitleResolver(ISharedGameTranslationRepository repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        _repo = repo;
    }

    public async Task<IReadOnlyList<SharedGameDto>> EnrichAsync(
        IReadOnlyList<SharedGameDto> games, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(games);

        if (games.Count == 0)
        {
            return games;
        }

        var ids = games.Select(g => g.Id).ToList();
        var translationsByGame = await _repo
            .GetByGameIdsAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        var enriched = new List<SharedGameDto>(games.Count);
        foreach (var game in games)
        {
            var translations = translationsByGame.TryGetValue(game.Id, out var ts)
                ? ts.Select(ToDto).ToList()
                : new List<SharedGameTranslationDto>(0);

            // `with` produces a shallow-cloned record preserving all other fields.
            enriched.Add(game with { Translations = translations });
        }

        return enriched;
    }

    private static SharedGameTranslationDto ToDto(SharedGameTranslation t) =>
        new(
            Locale: t.Locale.Value,
            Title: t.Title,
            Description: t.Description,
            Source: TranslationSourceMapper.ToPersistedString(t.Source));
}
