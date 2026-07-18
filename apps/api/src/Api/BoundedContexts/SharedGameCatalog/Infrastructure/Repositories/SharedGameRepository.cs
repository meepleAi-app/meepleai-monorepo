using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SharedGame aggregate.
/// </summary>
internal sealed class SharedGameRepository : RepositoryBase, ISharedGameRepository
{

    public SharedGameRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        var entity = MapToEntity(sharedGame);
        await DbContext.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Issue #3153 — persists a new <see cref="SharedGame"/> together with its
    /// designer/publisher M:N links resolved get-or-create BY NAME from the raw
    /// provenance names (NOT from the aggregate's throwaway-Guid collections).
    /// <see cref="MapToEntity"/> stays pure/scalar-only; the resolvers attach
    /// existing shared rows (case-insensitively, index-backed on <c>lower(name)</c>)
    /// or create new ones, deferring persistence to the caller's unit of work.
    /// </summary>
    public async Task AddAsync(
        SharedGame sharedGame,
        IReadOnlyList<string> designerNames,
        IReadOnlyList<string> publisherNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);

        var entity = MapToEntity(sharedGame);

        foreach (var designer in await ResolveDesignerEntitiesAsync(designerNames, cancellationToken).ConfigureAwait(false))
        {
            entity.Designers.Add(designer);
        }

        foreach (var publisher in await ResolvePublisherEntitiesAsync(publisherNames, cancellationToken).ConfigureAwait(false))
        {
            entity.Publishers.Add(publisher);
        }

        await DbContext.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get-or-create-by-name resolver for the designer M:N. Lenient (trims,
    /// skips empty / &gt;200-char names — never throws), case-insensitive dedup
    /// both within the incoming list and against the DB (via <c>lower(name)</c>,
    /// served by <c>ix_game_designers_name_lower</c>). Reuses already-tracked
    /// entities and attaches detached existing rows as <c>Unchanged</c> so EF does
    /// not attempt a duplicate INSERT. Issues NO <c>SaveChanges</c>.
    /// </summary>
    private async Task<List<GameDesignerEntity>> ResolveDesignerEntitiesAsync(
        IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        var resolved = new List<GameDesignerEntity>();
        if (names is null || names.Count == 0)
            return resolved;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var name = raw.Trim();
            if (name.Length > 200)
                continue;

            if (!seen.Add(name))
                continue; // within-list case-insensitive dedup

            // Already added/attached in this DbContext (batch / repeat safety).
            var tracked = DbContext.ChangeTracker.Entries<GameDesignerEntity>()
                .FirstOrDefault(e => string.Equals(e.Entity.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tracked is not null)
            {
                resolved.Add(tracked.Entity);
                continue;
            }

            var normalized = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862, MA0011 // d.Name.ToLower() is EF-translated to SQL lower(name) (served by ix_game_designers_name_lower), never run in-process
            var existing = await DbContext.GameDesigners
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name.ToLower() == normalized, cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore CA1304, CA1311, CA1862, MA0011

            if (existing is not null)
            {
                DbContext.Attach(existing);
                resolved.Add(existing);
                continue;
            }

            var created = new GameDesignerEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
            };
            await DbContext.GameDesigners.AddAsync(created, cancellationToken).ConfigureAwait(false);
            resolved.Add(created);
        }

        return resolved;
    }

    /// <summary>
    /// Get-or-create-by-name resolver for the publisher M:N — identical semantics
    /// to <see cref="ResolveDesignerEntitiesAsync"/> (index-backed on
    /// <c>ix_game_publishers_name_lower</c>). Issue #3153.
    /// </summary>
    private async Task<List<GamePublisherEntity>> ResolvePublisherEntitiesAsync(
        IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        var resolved = new List<GamePublisherEntity>();
        if (names is null || names.Count == 0)
            return resolved;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var name = raw.Trim();
            if (name.Length > 200)
                continue;

            if (!seen.Add(name))
                continue;

            var tracked = DbContext.ChangeTracker.Entries<GamePublisherEntity>()
                .FirstOrDefault(e => string.Equals(e.Entity.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tracked is not null)
            {
                resolved.Add(tracked.Entity);
                continue;
            }

            var normalized = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862, MA0011 // p.Name.ToLower() is EF-translated to SQL lower(name) (served by ix_game_publishers_name_lower), never run in-process
            var existing = await DbContext.GamePublishers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name.ToLower() == normalized, cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore CA1304, CA1311, CA1862, MA0011

            if (existing is not null)
            {
                DbContext.Attach(existing);
                resolved.Add(existing);
                continue;
            }

            var created = new GamePublisherEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
            };
            await DbContext.GamePublishers.AddAsync(created, cancellationToken).ConfigureAwait(false);
            resolved.Add(created);
        }

        return resolved;
    }

    public async Task<SharedGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Issue #2035: Include Designers so MapToDomain can hydrate the aggregate's
        // designer collection — required by GetGameDetailQueryHandler which surfaces
        // designer names on the library detail DTO.
        var entity = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .Include(g => g.Designers)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<SharedGame?> GetByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.BggId == bggId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public void Update(SharedGame sharedGame)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        var entity = MapToEntity(sharedGame);
        DbContext.Set<SharedGameEntity>().Update(entity);
    }

    public async Task<bool> ExistsByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .AnyAsync(g => g.BggId == bggId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .AnyAsync(g => g.Id == id && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, SharedGame>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, SharedGame>();

        var entities = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .Where(g => idList.Contains(g.Id) && !g.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.ToDictionary(
            e => e.Id,
            e => MapToDomain(e));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var rows = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .Where(g => ids.Contains(g.Id) && !g.IsDeleted)
            .Select(g => new { g.Id, g.Title })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.Id, r => r.Title);
    }

    // Mapping methods

    private static SharedGame MapToDomain(SharedGameEntity entity)
    {
        GameRules? rules = null;
        if (!string.IsNullOrEmpty(entity.RulesContent) && !string.IsNullOrEmpty(entity.RulesLanguage))
        {
            rules = GameRules.Create(entity.RulesContent, entity.RulesLanguage, entity.RulesExternalUrl);
        }
        else if (!string.IsNullOrEmpty(entity.RulesExternalUrl))
        {
            rules = GameRules.CreateFromUrl(entity.RulesExternalUrl);
        }

        // Use internal reconstruction constructor (no events)
        var sharedGame = new SharedGame(
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
            // Issue #2123 — entity.ImageUrl/ThumbnailUrl are now nullable post-Phase A
            // nullify migration. SharedGame aggregate still types them as non-nullable
            // (legacy contract; deprecation tombstone). Coerce to empty string here so
            // null entity values don't blow up the aggregate constructor — FE consumers
            // MUST prefer SharedGameDto.CoverUrl which is the R2-resolved replacement.
            entity.ImageUrl ?? string.Empty,
            entity.ThumbnailUrl ?? string.Empty,
            rules,
            (GameStatus)entity.Status,
            entity.CreatedBy,
            entity.ModifiedBy,
            entity.CreatedAt,
            entity.ModifiedAt,
            entity.IsDeleted,
            entity.BggId,
            entity.AgentDefinitionId,
            (GameDataStatus)entity.GameDataStatus,
            entity.HasUploadedPdf,
            pdfCoverR2Key: entity.PdfCoverR2Key,
            wikidataQid: entity.WikidataQid,
            wikidataCoverR2Key: entity.WikidataCoverR2Key,
            wikidataCoverLicense: entity.WikidataCoverLicense,
            wikidataCoverAttribution: entity.WikidataCoverAttribution,
            wikidataCoverSourceUrl: entity.WikidataCoverSourceUrl,
            wikidataQidLastVerifiedAt: entity.WikidataQidLastVerifiedAt);

        // Issue #2035: Hydrate designers from the M:N join — only when the caller
        // eager-loaded the navigation (GetByIdAsync), otherwise the EF Core
        // collection is empty by default (lazy loading is not enabled).
        foreach (var designer in entity.Designers)
        {
            if (!string.IsNullOrWhiteSpace(designer.Name))
            {
                sharedGame.AddDesigner(designer.Name);
            }
        }

        return sharedGame;
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
            GameDataStatus = (int)game.GameDataStatus,
            RulesContent = game.Rules?.Content,
            RulesLanguage = game.Rules?.Language,
            RulesExternalUrl = game.Rules?.ExternalUrl,
            HasUploadedPdf = game.HasUploadedPdf,
            PdfCoverR2Key = game.PdfCoverR2Key,
            // Issue #1823 Phase B M8 — propagate Wikidata enrichment state.
            WikidataQid = game.WikidataQid,
            WikidataCoverR2Key = game.WikidataCoverR2Key,
            WikidataCoverLicense = game.WikidataCoverLicense,
            WikidataCoverAttribution = game.WikidataCoverAttribution,
            WikidataCoverSourceUrl = game.WikidataCoverSourceUrl,
            WikidataQidLastVerifiedAt = game.WikidataQidLastVerifiedAt,
            // SearchVector managed by PostgreSQL trigger
            CreatedBy = game.CreatedBy,
            ModifiedBy = game.ModifiedBy,
            CreatedAt = game.CreatedAt,
            ModifiedAt = game.ModifiedAt,
            IsDeleted = game.IsDeleted,  // Fix: Use aggregate value, not hardcoded false (Issue #2514 code review)
            AgentDefinitionId = game.AgentDefinitionId  // Issue #4228
        };
    }

    public async Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.SharedGames
            .Include(g => g.Faqs)
            .Where(g => g.Faqs.Any(f => f.Id == faqId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<SharedGame?> GetGameByErrataIdAsync(Guid errataId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.SharedGames
            .Include(g => g.Erratas)
            .Where(g => g.Erratas.Any(e => e.Id == errataId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<SharedGame?> GetByIdWithDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.SharedGames
            .AsNoTracking()
            .IgnoreQueryFilters() // Include soft-deleted games
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await DbContext.SharedGames
            .AsNoTracking()
            .AnyAsync(g => EF.Functions.ILike(g.Title, title), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<SharedGame>> GetByGameDataStatusAsync(GameDataStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SharedGames
            .AsNoTracking()
            .Where(g => g.GameDataStatus == (int)status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
