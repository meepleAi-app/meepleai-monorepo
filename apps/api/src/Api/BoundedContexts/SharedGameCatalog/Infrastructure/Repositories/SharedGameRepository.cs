using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SharedGame aggregate.
/// </summary>
internal sealed class SharedGameRepository : ISharedGameRepository
{
    private readonly MeepleAiDbContext _context;

    public SharedGameRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        var entity = MapToEntity(sharedGame);
        await _context.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SharedGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<SharedGame?> GetByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.BggId == bggId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public void Update(SharedGame sharedGame)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        var entity = MapToEntity(sharedGame);
        _context.Set<SharedGameEntity>().Update(entity);
    }

    public async Task<bool> ExistsByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .AnyAsync(g => g.BggId == bggId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    // Mapping methods

    private static SharedGame MapToDomain(SharedGameEntity entity)
    {
        GameRules? rules = null;
        if (!string.IsNullOrEmpty(entity.RulesContent) && !string.IsNullOrEmpty(entity.RulesLanguage))
        {
            rules = GameRules.Create(entity.RulesContent, entity.RulesLanguage);
        }

        // Use internal reconstruction constructor (no events)
        return new SharedGame(
            entity.Id,
            entity.Title,
            entity.YearPublished,
            entity.Description,
            entity.MinPlayers,
            entity.MaxPlayers,
            entity.PlayingTimeMinutes,
            entity.MinAge,
            entity.ComplexityRating,
            entity.AverageRating,
            entity.ImageUrl,
            entity.ThumbnailUrl,
            rules,
            (GameStatus)entity.Status,
            entity.CreatedBy,
            entity.ModifiedBy,
            entity.CreatedAt,
            entity.ModifiedAt,
            entity.IsDeleted,
            entity.BggId);
    }

    private static SharedGameEntity MapToEntity(SharedGame game)
    {
        return new SharedGameEntity
        {
            Id = game.Id,
            BggId = game.BggId,
            Title = game.Title,
            YearPublished = game.YearPublished,
            Description = game.Description,
            MinPlayers = game.MinPlayers,
            MaxPlayers = game.MaxPlayers,
            PlayingTimeMinutes = game.PlayingTimeMinutes,
            MinAge = game.MinAge,
            ComplexityRating = game.ComplexityRating,
            AverageRating = game.AverageRating,
            ImageUrl = game.ImageUrl,
            ThumbnailUrl = game.ThumbnailUrl,
            Status = (int)game.Status,
            RulesContent = game.Rules?.Content,
            RulesLanguage = game.Rules?.Language,
            // SearchVector managed by PostgreSQL trigger
            CreatedBy = game.CreatedBy,
            ModifiedBy = game.ModifiedBy,
            CreatedAt = game.CreatedAt,
            ModifiedAt = game.ModifiedAt,
            IsDeleted = false
        };
    }

    public async Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await _context.SharedGames
            .Include(g => g.Faqs)
            .Where(g => g.Faqs.Any(f => f.Id == faqId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<SharedGame?> GetGameByErrataIdAsync(Guid errataId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await _context.SharedGames
            .Include(g => g.Erratas)
            .Where(g => g.Erratas.Any(e => e.Id == errataId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<SharedGame?> GetByIdWithDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gameEntity = await _context.SharedGames
            .AsNoTracking()
            .IgnoreQueryFilters() // Include soft-deleted games
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }
}
