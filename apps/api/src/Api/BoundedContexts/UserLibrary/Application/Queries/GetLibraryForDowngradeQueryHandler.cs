using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for GetLibraryForDowngradeQuery.
/// Returns a preview of which library entries would be kept vs removed when a user downgrades
/// to a lower quota tier.
/// Entries are sorted by priority: favorites first, then by times played (desc), then by last played / added date (desc).
/// </summary>
internal class GetLibraryForDowngradeQueryHandler : IQueryHandler<GetLibraryForDowngradeQuery, LibraryForDowngradeDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly MeepleAiDbContext _db;

    public GetLibraryForDowngradeQueryHandler(
        IUserLibraryRepository libraryRepository,
        MeepleAiDbContext db)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<LibraryForDowngradeDto> Handle(GetLibraryForDowngradeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch all library entries for this user (up to 1000 — reasonable upper bound for a personal library)
        var (entries, _) = await _libraryRepository.GetUserLibraryPaginatedAsync(
            userId: query.UserId,
            search: null,
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 1000,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return new LibraryForDowngradeDto(
                GamesToKeep: [],
                GamesToRemove: []
            );
        }

        // Fetch SharedGame metadata for all entries at once (single DB round-trip)
        var gameIds = entries.Select(e => e.GameId).Distinct().ToList();
        var sharedGames = await _db.SharedGames
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken)
            .ConfigureAwait(false);

        // Sort entries by priority:
        //   1. Favorites first
        //   2. Most played
        //   3. Most recently played (fall back to AddedAt if never played)
        var sorted = entries
            .OrderByDescending(e => e.IsFavorite)
            .ThenByDescending(e => e.Stats.TimesPlayed)
            .ThenByDescending(e => e.Stats.LastPlayed ?? e.AddedAt)
            .ToList();

        // Split at NewQuota
        var quota = Math.Max(0, query.NewQuota);
        var toKeep = sorted.Take(quota).ToList();
        var toRemove = sorted.Skip(quota).ToList();

        return new LibraryForDowngradeDto(
            GamesToKeep: toKeep.Select(e => ToDto(e, sharedGames)).ToList(),
            GamesToRemove: toRemove.Select(e => ToDto(e, sharedGames)).ToList()
        );
    }

    private static LibraryDowngradeGameDto ToDto(
        Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry entry,
        Dictionary<Guid, Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity> sharedGames)
    {
        sharedGames.TryGetValue(entry.GameId, out var game);

        return new LibraryDowngradeGameDto(
            EntryId: entry.Id,
            GameId: entry.GameId,
            GameTitle: game?.Title ?? "Unknown Game",
            GameImageUrl: string.IsNullOrEmpty(game?.ImageUrl) ? null : game.ImageUrl,
            IsFavorite: entry.IsFavorite,
            TimesPlayed: entry.Stats.TimesPlayed,
            AddedAt: entry.AddedAt,
            LastPlayedAt: entry.Stats.LastPlayed
        );
    }
}
