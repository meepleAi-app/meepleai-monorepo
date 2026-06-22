using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.Infrastructure;

/// <summary>
/// Resolves a <see cref="GameRef"/> by querying either the SharedGame or PrivateGame repository.
/// Replaces the legacy <c>IGameRepository.GetByIdOrSharedGameIdAsync</c> dual-fallback pattern (issue #1320).
/// </summary>
/// <remarks>
/// Maps aggregate properties to <see cref="GameCoreData"/> inline.
/// Once SharedGame/PrivateGame are refactored to embed GameCoreData (P2-T5, P2-T6),
/// the mapping helpers below collapse to <c>aggregate.CoreData</c>.
/// </remarks>
internal sealed class GameCoreDataProvider : IGameCoreDataProvider
{
    private readonly ISharedGameRepository _sharedGames;
    private readonly IPrivateGameRepository _privateGames;

    public GameCoreDataProvider(
        ISharedGameRepository sharedGames,
        IPrivateGameRepository privateGames)
    {
        _sharedGames = sharedGames;
        _privateGames = privateGames;
    }

    public async Task<GameCoreData?> GetCoreDataAsync(
        GameRef gameRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameRef);

        return gameRef.Kind switch
        {
            GameRefKind.Shared => MapShared(await _sharedGames.GetByIdAsync(gameRef.Id, cancellationToken).ConfigureAwait(false)),
            GameRefKind.Private => MapPrivate(await _privateGames.GetByIdAsync(gameRef.Id, cancellationToken).ConfigureAwait(false)),
            _ => throw new ArgumentOutOfRangeException(nameof(gameRef), gameRef.Kind, "Unsupported GameRefKind")
        };
    }

    public async Task<IReadOnlyDictionary<GameRef, GameCoreData>> GetCoreDataBatchAsync(
        IReadOnlyCollection<GameRef> gameRefs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameRefs);

        var result = new Dictionary<GameRef, GameCoreData>();
        foreach (var r in gameRefs.Distinct())
        {
            var data = await GetCoreDataAsync(r, cancellationToken).ConfigureAwait(false);
            if (data is not null)
                result[r] = data;
        }
        return result;
    }

    // SharedGame fields are non-nullable: Title (string), YearPublished (int), Description (string),
    // MinPlayers (int), MaxPlayers (int), PlayingTimeMinutes (int), MinAge (int),
    // ImageUrl (string), ThumbnailUrl (string), BggId (int?), ComplexityRating (decimal?)
    private static GameCoreData? MapShared(SharedGame? sg) =>
        sg is null ? null : GameCoreData.Create(
            title: sg.Title,
            yearPublished: sg.YearPublished,
            minPlayers: sg.MinPlayers,
            maxPlayers: sg.MaxPlayers,
            playingTimeMinutes: sg.PlayingTimeMinutes,
            minAge: sg.MinAge,
            description: sg.Description,
            imageUrl: sg.ImageUrl,
            thumbnailUrl: sg.ThumbnailUrl,
            bggId: sg.BggId,
            complexityRating: sg.ComplexityRating);

    // PrivateGame fields: Title (string non-null), YearPublished (int?), Description (string?),
    // MinPlayers (int), MaxPlayers (int), PlayingTimeMinutes (int?), MinAge (int?),
    // ImageUrl (string?), ThumbnailUrl (string?), BggId (int?), ComplexityRating (decimal?)
    private static GameCoreData? MapPrivate(PrivateGame? pg) =>
        pg is null ? null : GameCoreData.Create(
            title: pg.Title,
            yearPublished: pg.YearPublished ?? 0,
            minPlayers: pg.MinPlayers,
            maxPlayers: pg.MaxPlayers,
            playingTimeMinutes: pg.PlayingTimeMinutes ?? 0,
            minAge: pg.MinAge ?? 0,
            description: pg.Description,
            imageUrl: pg.ImageUrl,
            thumbnailUrl: pg.ThumbnailUrl,
            bggId: pg.BggId,
            complexityRating: pg.ComplexityRating);
}
