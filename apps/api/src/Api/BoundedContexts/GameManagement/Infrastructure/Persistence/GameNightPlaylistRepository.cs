using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameNightPlaylist repository.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
internal class GameNightPlaylistRepository : RepositoryBase, IGameNightPlaylistRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GameNightPlaylistRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameNightPlaylist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightPlaylists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GameNightPlaylist>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightPlaylists
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<(IReadOnlyList<GameNightPlaylist> Playlists, int Total)> GetByCreatorPaginatedAsync(
        Guid creatorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.GameNightPlaylists
            .AsNoTracking()
            .Where(p => p.CreatorUserId == creatorUserId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var playlists = entities.Select(MapToDomain).ToList();
        return (playlists, total);
    }

    public async Task<GameNightPlaylist?> GetByShareTokenAsync(
        string shareToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shareToken);

        var entity = await DbContext.GameNightPlaylists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ShareToken == shareToken && p.IsShared, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(GameNightPlaylist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        CollectDomainEvents(playlist);

        var entity = MapToPersistence(playlist);
        await DbContext.GameNightPlaylists.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameNightPlaylist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        CollectDomainEvents(playlist);

        var entity = MapToPersistence(playlist);
        DbContext.GameNightPlaylists.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GameNightPlaylist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        var entity = MapToPersistence(playlist);
        DbContext.GameNightPlaylists.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameNightPlaylists
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static GameNightPlaylist MapToDomain(GameNightPlaylistEntity entity)
    {
        var playlist = new GameNightPlaylist(
            id: entity.Id,
            name: entity.Name,
            creatorUserId: entity.CreatorUserId,
            scheduledDate: entity.ScheduledDate);

        // Restore state from persistence
        var createdAtProp = typeof(GameNightPlaylist).GetProperty("CreatedAt");
        createdAtProp?.SetValue(playlist, entity.CreatedAt);

        var updatedAtProp = typeof(GameNightPlaylist).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(playlist, entity.UpdatedAt);

        var isDeletedProp = typeof(GameNightPlaylist).GetProperty("IsDeleted");
        isDeletedProp?.SetValue(playlist, entity.IsDeleted);

        var deletedAtProp = typeof(GameNightPlaylist).GetProperty("DeletedAt");
        deletedAtProp?.SetValue(playlist, entity.DeletedAt);

        var shareTokenProp = typeof(GameNightPlaylist).GetProperty("ShareToken");
        shareTokenProp?.SetValue(playlist, entity.ShareToken);

        var isSharedProp = typeof(GameNightPlaylist).GetProperty("IsShared");
        isSharedProp?.SetValue(playlist, entity.IsShared);

        var rowVersionProp = typeof(GameNightPlaylist).GetProperty("RowVersion");
        rowVersionProp?.SetValue(playlist, entity.RowVersion);

        // Restore games from JSON
        if (!string.IsNullOrEmpty(entity.GamesJson) && !string.Equals(entity.GamesJson, "[]", StringComparison.Ordinal))
        {
            var games = JsonSerializer.Deserialize<List<PlaylistGame>>(entity.GamesJson, JsonOptions)
                ?? new List<PlaylistGame>();
            playlist.RestoreGames(games);
        }

        // Clear domain events from reconstruction
        playlist.ClearDomainEvents();

        return playlist;
    }

    private static GameNightPlaylistEntity MapToPersistence(GameNightPlaylist domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        return new GameNightPlaylistEntity
        {
            Id = domainEntity.Id,
            Name = domainEntity.Name,
            ScheduledDate = domainEntity.ScheduledDate,
            CreatorUserId = domainEntity.CreatorUserId,
            ShareToken = domainEntity.ShareToken,
            IsShared = domainEntity.IsShared,
            GamesJson = JsonSerializer.Serialize(domainEntity.Games.ToList(), JsonOptions),
            IsDeleted = domainEntity.IsDeleted,
            DeletedAt = domainEntity.DeletedAt,
            CreatedAt = domainEntity.CreatedAt,
            UpdatedAt = domainEntity.UpdatedAt,
            RowVersion = domainEntity.RowVersion
        };
    }
}
