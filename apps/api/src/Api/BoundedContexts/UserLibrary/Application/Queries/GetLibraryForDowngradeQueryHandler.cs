using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal sealed class GetLibraryForDowngradeQueryHandler : IQueryHandler<GetLibraryForDowngradeQuery, LibraryForDowngradeDto>
{
    private readonly IUserLibraryRepository _repository;
    private readonly MeepleAiDbContext _db;

    public GetLibraryForDowngradeQueryHandler(IUserLibraryRepository repository, MeepleAiDbContext db)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(db);
        _repository = repository;
        _db = db;
    }

    public async Task<LibraryForDowngradeDto> Handle(GetLibraryForDowngradeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await _repository.GetUserGamesAsync(query.UserId, null, cancellationToken).ConfigureAwait(false);

        // Priority sort: favorites first > most played > most recently added
        var sorted = all
            .OrderByDescending(e => e.IsFavorite)
            .ThenByDescending(e => e.Stats.TimesPlayed)
            .ThenByDescending(e => e.Stats.LastPlayed ?? e.AddedAt)
            .ToList();

        // Fetch game titles/images from catalog (cross-context lookup)
        var gameIds = sorted.Select(e => e.GameId).ToList();
        var sharedGames = await _db.SharedGames
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken)
            .ConfigureAwait(false);

        var toKeep = sorted.Take(query.NewQuota).ToList();
        var toRemove = sorted.Skip(query.NewQuota).ToList();

        return new LibraryForDowngradeDto(
            GamesToKeep: toKeep.Select(e => MapToDto(e, sharedGames)).ToList(),
            GamesToRemove: toRemove.Select(e => MapToDto(e, sharedGames)).ToList()
        );
    }

    private static LibraryDowngradeGameDto MapToDto(
        UserLibraryEntry entry,
        Dictionary<Guid, SharedGameEntity> sharedGames)
    {
        sharedGames.TryGetValue(entry.GameId, out var game);
        return new LibraryDowngradeGameDto(
            EntryId: entry.Id,
            GameId: entry.GameId,
            GameTitle: game?.Title ?? "Unknown Game",
            GameImageUrl: game?.ThumbnailUrl,
            IsFavorite: entry.IsFavorite,
            TimesPlayed: entry.Stats.TimesPlayed,
            AddedAt: entry.AddedAt,
            LastPlayedAt: entry.Stats.LastPlayed
        );
    }
}
