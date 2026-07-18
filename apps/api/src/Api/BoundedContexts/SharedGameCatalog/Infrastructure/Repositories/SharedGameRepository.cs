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
        // Issue #3153 — persist the aggregate's designers/publishers as M:N join rows
        // (get-or-create by name). MapToEntity only maps scalar columns, so without this
        // the aggregate's designer/publisher membership was silently dropped on write.
        await ResolveDesignersAsync(entity, sharedGame.Designers, cancellationToken).ConfigureAwait(false);
        await ResolvePublishersAsync(entity, sharedGame.Publishers, cancellationToken).ConfigureAwait(false);
        await DbContext.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    // Issue #3153 — get-or-create each designer by name against the unique game_designers
    // table and attach the resolved row to the new SharedGameEntity so the caller's single
    // SaveChanges inserts the join rows. Mirrors RelationshipSeeder.GetOrCreateDesignerAsync.
    // No SaveChanges here — the caller (event handler / command handler) owns the flush.
    private async Task ResolveDesignersAsync(
        SharedGameEntity entity,
        IReadOnlyCollection<GameDesigner> designers,
        CancellationToken cancellationToken)
    {
        // De-dup by trimmed name (case-insensitive) so the same name supplied twice in one
        // call cannot insert two rows (CreateSharedGameCommand accepts up to 20 names with
        // no de-dup) → would otherwise violate the UNIQUE ix_game_designers_name.
        foreach (var trimmed in designers
            .Select(d => d.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Case-insensitive match against the unique game_designers table. The %/_ LIKE
            // metacharacters are escaped so a name that literally contains them (e.g. "50%")
            // matches on its literal text instead of over-matching an unrelated row.
            var pattern = EscapeLike(trimmed);
            var existing = await DbContext.GameDesigners
                .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Name, pattern, LikeEscapeChar), cancellationToken)
                .ConfigureAwait(false);

            var resolved = existing
                ?? DbContext.GameDesigners.Local.FirstOrDefault(
                       d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                ?? new GameDesignerEntity { Id = Guid.NewGuid(), Name = trimmed, CreatedAt = DateTime.UtcNow };

            if (!entity.Designers.Any(d => d.Id == resolved.Id))
            {
                entity.Designers.Add(resolved);
            }
        }
    }

    // Issue #3153 — symmetric get-or-create for publishers against game_publishers.
    private async Task ResolvePublishersAsync(
        SharedGameEntity entity,
        IReadOnlyCollection<GamePublisher> publishers,
        CancellationToken cancellationToken)
    {
        foreach (var trimmed in publishers
            .Select(p => p.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var pattern = EscapeLike(trimmed);
            var existing = await DbContext.GamePublishers
                .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Name, pattern, LikeEscapeChar), cancellationToken)
                .ConfigureAwait(false);

            var resolved = existing
                ?? DbContext.GamePublishers.Local.FirstOrDefault(
                       p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                ?? new GamePublisherEntity { Id = Guid.NewGuid(), Name = trimmed, CreatedAt = DateTime.UtcNow };

            if (!entity.Publishers.Any(p => p.Id == resolved.Id))
            {
                entity.Publishers.Add(resolved);
            }
        }
    }

    // Issue #3153 — escape LIKE metacharacters (\ % _) so an ILIKE pattern matches its
    // argument as literal text (case-insensitively) rather than treating %/_ as wildcards.
    private const string LikeEscapeChar = "\\";
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<SharedGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Issue #2035 / #3153: Include Designers + Publishers so MapToDomain can hydrate the
        // aggregate's M:N collections — designer names feed GetGameDetailQueryHandler's
        // library detail DTO; publisher hydration keeps the write/read round-trip symmetric.
        // AsSplitQuery avoids the cartesian product from the two collection includes.
        var entity = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .Include(g => g.Designers)
            .Include(g => g.Publishers)
            .AsSplitQuery()
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

        // Issue #3153: hydrate publishers symmetrically with designers — only when the
        // caller eager-loaded the navigation (GetByIdAsync), otherwise empty by default.
        foreach (var publisher in entity.Publishers)
        {
            if (!string.IsNullOrWhiteSpace(publisher.Name))
            {
                sharedGame.AddPublisher(publisher.Name);
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
