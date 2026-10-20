using System.Runtime.CompilerServices;
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
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SharedGame aggregate.
/// </summary>
internal sealed class SharedGameRepository : RepositoryBase, ISharedGameRepository
{
    /// <summary>
    /// Le colonne di <c>shared_games</c> che l'aggregato non porta (#4585). <see cref="Update"/> non le
    /// scrive mai: ci pensa chi le possiede, sull'entità.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DbSet.Update</c> su un grafo detached segna come modificata ogni colonna, e una colonna che
    /// <c>MapToEntity</c> non copia arrivava al database al valore di default della classe. Valeva per
    /// tutte le scritture del catalogo, non solo per quelle che le riguardavano. Stesso schema di
    /// <c>PdfDocumentRepository.ColumnsNotOwnedByTheAggregate</c> (#4347).
    /// </para>
    /// <list type="bullet">
    /// <item><c>HasKnowledgeBase</c>: la proiezione dell'indicizzazione
    /// (<c>VectorDocumentIndexedForKbFlagHandler</c>) e <c>KbFlagDriftAuditJob</c>.</item>
    /// <item><c>BggRawData</c>: la risposta grezza di BGG, per ripopolare il gioco. Nessun codice del
    /// prodotto la scrive sull'aggregato.</item>
    /// <item><c>TestRunId</c>: il marcatore dei giochi di test (<c>SeedTestLibraryGameCommandHandler</c>),
    /// su cui si appoggia <c>CleanupTestEntitiesCommandHandler</c>.</item>
    /// </list>
    /// <para>
    /// <c>IsRagPublic</c> non è qui: l'aggregato lo porta (<c>SetRagPublicAccess</c>), quindi lo legge
    /// <c>MapToDomain</c> e lo scrive <c>MapToEntity</c>. Escluderlo farebbe perdere
    /// <c>PUT …/rag-access</c> in silenzio.
    /// </para>
    /// <para>
    /// Il test <c>SharedGameRepositoryUnownedColumnsTests</c> prende le colonne dal modello EF: una colonna
    /// aggiunta domani all'entità, senza mapping e senza essere qui, lo fa fallire con il suo nome.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyList<string> ColumnsNotOwnedByTheAggregate =
    [
        nameof(SharedGameEntity.HasKnowledgeBase),
        nameof(SharedGameEntity.BggRawData),
        nameof(SharedGameEntity.TestRunId),
    ];

    public SharedGameRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        var entity = MapToEntity(sharedGame);
        AddChildRows(entity, sharedGame);
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
        AddChildRows(entity, sharedGame);

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
        // Epic #3470: Include CoverAssignments so the aggregate carries its per-context
        // admin cover overrides (≤3 rows) for the context-aware resolver + write path.
        // AsSplitQuery: two collection includes (Designers M:N + CoverAssignments 1:N)
        // would otherwise form a cartesian product that, under AsNoTracking (no identity
        // resolution), DUPLICATES the child rows in each navigation. Split queries issue
        // one SELECT per collection instead — matches MechanicAnalysisRepository.
        var entity = await DbContext.Set<SharedGameEntity>()
            .AsNoTracking()
            .Include(g => g.Designers)
            .Include(g => g.CoverAssignments)
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

        // MapToEntity porta la sola radice: Update() su un grafo detached marcherebbe Modified anche
        // un figlio nuovo, che ha già l'id — un UPDATE di una riga che non esiste. Errata (#4543) e
        // FAQ (#4557) passano da ReconcileChildren.
        var entity = MapToEntity(sharedGame);
        var entry = DbContext.Set<SharedGameEntity>().Update(entity);

        // #4585: Update() segna come modificate anche le colonne che il mapper lascia al default. Quelle
        // restano come sono nella riga. Nell'entità che resta nel contesto però valgono il default: chi la
        // rilegge tracciata nello stesso contesto le sovrascriva, non le usi. L'UPDATE della radice parte
        // comunque, perché le altre colonne restano modificate: è quello che porta il controllo su xmin.
        foreach (var column in ColumnsNotOwnedByTheAggregate)
        {
            entry.Property(column).IsModified = false;
        }

        ErrataReadBy.TryGetValue(sharedGame, out var errataRead);
        ReconcileChildren(errataRead, sharedGame.Erratas.ToDictionary(e => e.Id, MapErrataToEntity), CopyOf);

        FaqsReadBy.TryGetValue(sharedGame, out var faqsRead);
        ReconcileChildren(faqsRead, sharedGame.Faqs.ToDictionary(f => f.Id, MapFaqToEntity), CopyOf);
    }

    /// <summary>
    /// Le errata che una lettura ha messo nell'aggregato, per id, come le ha trovate (#4543).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Update"/> riceve solo l'aggregato, e l'aggregato da solo non dice se la sua lista
    /// di errata è vuota o non è stata letta: <see cref="GetByIdAsync"/>, la lettura di quasi ogni
    /// comando sullo <c>SharedGame</c>, non le carica. Cancellare «ciò che è nel database e non
    /// nell'aggregato» cancellerebbe tutte le errata a ogni salvataggio. Si cancella quindi solo ciò
    /// che la lettura ha caricato e il dominio ha tolto; una riga che la lettura non ha visto non si
    /// tocca.
    /// </para>
    /// <para>
    /// Un aggregato assente dalla tabella non ha letto le errata: quelle che porta sono nate nel
    /// dominio dopo la lettura, e diventano INSERT. Stesso schema di <c>UserLibraryRepository</c>
    /// (#4505). La chiave è l'istanza dell'aggregato, tenuta per riferimento debole: la voce sparisce
    /// con lui, e un aggregato letto da un'istanza del repository e salvato da un'altra resta
    /// corretto.
    /// </para>
    /// </remarks>
    private static readonly ConditionalWeakTable<SharedGame, IReadOnlyDictionary<Guid, GameErrataEntity>> ErrataReadBy = new();

    /// <summary>
    /// Le FAQ che una lettura ha messo nell'aggregato, per id, come le ha trovate (#4557). Stesse
    /// ragioni di <see cref="ErrataReadBy"/>: una tabella per figlio, perché una lettura può caricare
    /// le errata senza le FAQ, o il contrario.
    /// </summary>
    private static readonly ConditionalWeakTable<SharedGame, IReadOnlyDictionary<Guid, GameFaqEntity>> FaqsReadBy = new();

    /// <summary>
    /// Porta nel change tracker le righe figlie dell'aggregato, errata (#4543) o FAQ (#4557):
    /// <c>Added</c> quelle che la lettura non aveva; quelle lette e ancora presenti partono dai valori
    /// letti e ricevono quelli del dominio, così EF scrive solo le colonne cambiate (nessuna, nessun
    /// UPDATE); quelle lette e tolte dal dominio diventano <c>Deleted</c>.
    /// </summary>
    /// <param name="read">Le righe che la lettura ha caricato, per id; <c>null</c> se non le ha
    /// caricate. In quel caso nessuna riga si cancella e nessuna si aggiorna.</param>
    /// <param name="desired">Le righe come le vuole il dominio, per id.</param>
    /// <param name="copyOf">Una copia scollegata di una riga letta.</param>
    /// <remarks>
    /// Errata e FAQ non hanno un token di concorrenza: una scrittura da una lettura superata fallisce
    /// per intero perché l'UPDATE della radice, nella stessa transazione, controlla <c>xmin</c>. Quella
    /// UPDATE c'è sempre perché <c>Update()</c> su un grafo detached marca ogni colonna della radice:
    /// se un giorno la radice si salvasse solo quando cambia, i figli resterebbero senza protezione.
    /// Lo fissano <c>ConcurrentErrataUpdates_TheSecondSaveFailsOnTheGameXmin</c> e
    /// <c>ConcurrentFaqUpdates_TheSecondSaveFailsOnTheGameXmin</c>.
    /// </remarks>
    private void ReconcileChildren<TRow>(
        IReadOnlyDictionary<Guid, TRow>? read,
        IReadOnlyDictionary<Guid, TRow> desired,
        Func<TRow, TRow> copyOf)
        where TRow : class
    {
        foreach (var (id, row) in desired)
        {
            if (read is null || !read.TryGetValue(id, out var readRow))
            {
                DbContext.Entry(row).State = EntityState.Added;
                continue;
            }

            TrackAsRead(id, readRow, copyOf).CurrentValues.SetValues(row);
        }

        if (read is null)
        {
            return;
        }

        foreach (var (id, readRow) in read)
        {
            if (!desired.ContainsKey(id))
            {
                TrackAsRead(id, readRow, copyOf).State = EntityState.Deleted;
            }
        }
    }

    /// <summary>
    /// La riga figlia nel change tracker, con i valori letti come originali. Se il contesto la
    /// traccia già usa quella: attaccarne una seconda con la stessa chiave fallirebbe.
    /// </summary>
    private EntityEntry<TRow> TrackAsRead<TRow>(Guid id, TRow row, Func<TRow, TRow> copyOf)
        where TRow : class
    {
        var tracked = DbContext.Set<TRow>().Local.FindEntry(id);
        if (tracked is not null)
        {
            return tracked;
        }

        // Una copia nuova a ogni salvataggio: la stessa istanza attaccata a due contesti
        // porterebbe nel secondo lo stato del primo.
        var entry = DbContext.Entry(copyOf(row));
        entry.State = EntityState.Unchanged;
        return entry;
    }

    /// <summary>
    /// #4543, #4557 — errata e FAQ di un aggregato nuovo: tutto il grafo è <c>Added</c>, quindi qui
    /// vanno nel grafo, a differenza di <see cref="Update"/>.
    /// </summary>
    private static void AddChildRows(SharedGameEntity entity, SharedGame sharedGame)
    {
        foreach (var errata in sharedGame.Erratas)
        {
            entity.Erratas.Add(MapErrataToEntity(errata));
        }

        foreach (var faq in sharedGame.Faqs)
        {
            entity.Faqs.Add(MapFaqToEntity(faq));
        }
    }

    private static GameErrataEntity MapErrataToEntity(GameErrata errata) => new()
    {
        Id = errata.Id,
        SharedGameId = errata.SharedGameId,
        Description = errata.Description,
        PageReference = errata.PageReference,
        PublishedDate = errata.PublishedDate,
        CreatedAt = errata.CreatedAt,
    };

    private static GameErrataEntity CopyOf(GameErrataEntity row) => new()
    {
        Id = row.Id,
        SharedGameId = row.SharedGameId,
        Description = row.Description,
        PageReference = row.PageReference,
        PublishedDate = row.PublishedDate,
        CreatedAt = row.CreatedAt,
    };

    private static GameFaqEntity MapFaqToEntity(GameFaq faq) => new()
    {
        Id = faq.Id,
        SharedGameId = faq.SharedGameId,
        Question = faq.Question,
        Answer = faq.Answer,
        DisplayOrder = faq.DisplayOrder,
        UpvoteCount = faq.UpvoteCount,
        CreatedAt = faq.CreatedAt,
        UpdatedAt = faq.UpdatedAt,
    };

    private static GameFaqEntity CopyOf(GameFaqEntity row) => new()
    {
        Id = row.Id,
        SharedGameId = row.SharedGameId,
        Question = row.Question,
        Answer = row.Answer,
        DisplayOrder = row.DisplayOrder,
        UpvoteCount = row.UpvoteCount,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };

    /// <summary>
    /// Epic #3470 — child-safe reconcile of the per-context cover assignments.
    /// Loads the current DB rows with <c>.AsTracking()</c> — the DbContext default is
    /// <c>QueryTrackingBehavior.NoTracking</c> (PERF-06), so WITHOUT this override an
    /// in-place mutation of a fetched row would be a silent no-op at SaveChanges
    /// (<c>notracking-default-update-gotcha</c>). Tracking also lets the in-place
    /// update carry the loaded <c>xmin</c> into the concurrency <c>WHERE</c> clause
    /// (ADR-060). Assignments new since the load are inserted, and rows the aggregate
    /// dropped are deleted. A detached full-graph <c>Update()</c> would instead mark a
    /// newly-added row (fresh <c>Guid.NewGuid()</c>) as Modified — an UPDATE against a
    /// nonexistent row that silently loses the insert on Postgres (InMemory hides it).
    /// No <c>SaveChanges</c> here — the caller's unit of work commits.
    /// </summary>
    public async Task ReconcileCoverAssignmentsAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);

        var set = DbContext.Set<GameCoverAssignmentEntity>();

        var existing = await set
            .AsTracking()
            .Where(a => a.SharedGameId == sharedGame.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingById = existing.ToDictionary(e => e.Id);

        var desiredIds = new HashSet<Guid>();
        foreach (var assignment in sharedGame.CoverAssignments)
        {
            desiredIds.Add(assignment.Id);

            if (existingById.TryGetValue(assignment.Id, out var tracked))
            {
                // Update in place — Context / CreatedAt / CreatedBy are immutable.
                // Mutating the tracked entity preserves its loaded xmin OriginalValue.
                tracked.Source = assignment.Source;
                tracked.FocalX = assignment.FocalX;
                tracked.FocalY = assignment.FocalY;
                tracked.GeneratedR2Key = assignment.GeneratedR2Key;
                tracked.UpdatedAt = assignment.UpdatedAt;
                tracked.UpdatedBy = assignment.UpdatedBy;
            }
            else
            {
                set.Add(MapAssignmentToEntity(assignment));
            }
        }

        foreach (var tracked in existing)
        {
            if (!desiredIds.Contains(tracked.Id))
            {
                set.Remove(tracked);
            }
        }
    }

    public async Task<int> InvalidateGeneratedCropsAsync(
        Guid gameId,
        CoverAssignmentSource source,
        CancellationToken cancellationToken = default)
    {
        // Filtered on GeneratedR2Key != null so the statement is a no-op (0 rows) for the common
        // case of a game with no rendered crop — which is every game until an admin pins a Social
        // assignment. Cheaper than loading the collection to find out there was nothing to do.
        return await DbContext.Set<GameCoverAssignmentEntity>()
            .Where(a => a.SharedGameId == gameId
                        && a.Source == source
                        && a.GeneratedR2Key != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.GeneratedR2Key, (string?)null)
                      .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
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
            wikidataQidLastVerifiedAt: entity.WikidataQidLastVerifiedAt,
            manualCoverR2Key: entity.ManualCoverR2Key,
            manualCoverLicense: entity.ManualCoverLicense,
            manualCoverAttribution: entity.ManualCoverAttribution,
            manualCoverSourceUrl: entity.ManualCoverSourceUrl,
            manualCoverAttestedBy: entity.ManualCoverAttestedBy,
            manualCoverAttestedAt: entity.ManualCoverAttestedAt,
            // #3590 Slice B — senza questa lettura l'aggregato non conosce la cover BGG, quindi
            // MapToEntity la riscriverebbe come NULL al primo Update() (grafo detached).
            bggCoverR2Key: entity.BggCoverR2Key,
            // #3651 — il token di concorrenza deve attraversare l'aggregato per la stessa ragione
            // strutturale delle colonne qui sopra: l'Update() è su grafo detached, quindi EF non ha
            // una riga tracciata da cui ricavare l'original value e usa quello sulla proprietà.
            // Senza questa lettura sarebbe 0, e ogni UPDATE emetterebbe `WHERE xmin = 0` — che non
            // corrisponde mai a un xid reale — fallendo anche senza alcuna concorrenza.
            xminVersion: entity.Xmin,
            // #4585 — SetRagPublicAccess cambia questo campo: senza la lettura l'aggregato partirebbe
            // sempre da false, e senza la scrittura in MapToEntity il valore nuovo non arriverebbe alla riga.
            isRagPublic: entity.IsRagPublic);

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

        // Epic #3470: Hydrate per-context cover assignments (child collection). Empty
        // unless the caller eager-loaded the navigation (GetByIdAsync). Reconstituted
        // via the internal ctor — no re-validation, no events.
        foreach (var ca in entity.CoverAssignments)
        {
            sharedGame.LoadCoverAssignment(new GameCoverAssignment(
                ca.Id,
                ca.SharedGameId,
                ca.Context,
                ca.Source,
                ca.FocalX,
                ca.FocalY,
                ca.GeneratedR2Key,
                ca.CreatedAt,
                ca.CreatedBy,
                ca.UpdatedAt,
                ca.UpdatedBy));
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
            // #3590 Slice B — ometterla qui la azzerava a ogni Update(): l'update è su grafo
            // detached, quindi marca OGNI colonna come Modified. Stessa ragione già annotata
            // sotto per le colonne manual.
            BggCoverR2Key = game.BggCoverR2Key,
            // Issue #1823 Phase B M8 — propagate Wikidata enrichment state.
            WikidataQid = game.WikidataQid,
            WikidataCoverR2Key = game.WikidataCoverR2Key,
            WikidataCoverLicense = game.WikidataCoverLicense,
            WikidataCoverAttribution = game.WikidataCoverAttribution,
            WikidataCoverSourceUrl = game.WikidataCoverSourceUrl,
            WikidataQidLastVerifiedAt = game.WikidataQidLastVerifiedAt,
            // Epic #3470 Slice 3a — manual cover columns (omitting them here would zero them on update).
            ManualCoverR2Key = game.ManualCoverR2Key,
            ManualCoverLicense = game.ManualCoverLicense,
            ManualCoverAttribution = game.ManualCoverAttribution,
            ManualCoverSourceUrl = game.ManualCoverSourceUrl,
            ManualCoverAttestedBy = game.ManualCoverAttestedBy,
            ManualCoverAttestedAt = game.ManualCoverAttestedAt,
            // SearchVector managed by PostgreSQL trigger
            CreatedBy = game.CreatedBy,
            ModifiedBy = game.ModifiedBy,
            CreatedAt = game.CreatedAt,
            ModifiedAt = game.ModifiedAt,
            IsDeleted = game.IsDeleted,  // Fix: Use aggregate value, not hardcoded false (Issue #2514 code review)
            AgentDefinitionId = game.AgentDefinitionId,  // Issue #4228
            IsRagPublic = game.IsRagPublic,  // #4585 — SetRagPublicAccess lo cambia; omessa, la riga restava a false
            // #3651 — token di concorrenza (ADR-060). Su un grafo detached EF non ha una riga
            // tracciata da cui ricavare l'original value del token e usa quello che trova qui:
            // omettendolo resterebbe 0, e ogni UPDATE emetterebbe `WHERE xmin = 0` colpendo 0
            // righe, cioè un DbUpdateConcurrencyException su ogni scrittura anche senza
            // concorrenza. Con il valore letto in MapToDomain il confronto è quello giusto: la
            // scrittura passa se la riga non è cambiata, e fallisce con 409 se qualcuno l'ha
            // modificata nel frattempo.
            Xmin = game.XminVersion
        };
    }

    /// <summary>Epic #3470 — maps a domain cover assignment to its persistence entity (INSERT path).</summary>
    private static GameCoverAssignmentEntity MapAssignmentToEntity(GameCoverAssignment assignment) =>
        new()
        {
            Id = assignment.Id,
            SharedGameId = assignment.SharedGameId,
            Context = assignment.Context,
            Source = assignment.Source,
            FocalX = assignment.FocalX,
            FocalY = assignment.FocalY,
            GeneratedR2Key = assignment.GeneratedR2Key,
            CreatedAt = assignment.CreatedAt,
            CreatedBy = assignment.CreatedBy,
            UpdatedAt = assignment.UpdatedAt,
            UpdatedBy = assignment.UpdatedBy,
        };

    /// <remarks>
    /// #4557: l'unica lettura che carica le FAQ, perché modificarne, toglierne o votarne una richiede
    /// di averla nell'aggregato. Stesse scelte di <see cref="GetGameByErrataIdAsync"/>: le carica
    /// tutte, solo qui, e <c>AddFaq</c> funziona anche senza (la FAQ nuova diventa INSERT).
    /// Il dettaglio del gioco (<c>GetSharedGameByIdQueryHandler</c>) e la lista pubblica delle FAQ
    /// (<c>GetGameFaqsQueryHandler</c>) leggono <c>game_faqs</c> direttamente dal DbContext, non da qui (#4565).
    /// </remarks>
    public async Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.SharedGames
            .AsNoTracking()
            .Include(g => g.Faqs)
            .Where(g => g.Faqs.Any(f => f.Id == faqId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomainWithFaqs(gameEntity) : null;
    }

    /// <remarks>
    /// #4557: il voto è un contatore su <c>game_faqs</c>, non una modifica del gioco, quindi non passa
    /// da <see cref="Update"/>. L'update dell'aggregato riscrive la riga di <c>shared_games</c> e passa
    /// dal controllo su <c>xmin</c>, cioè due voti insieme finivano in 409. Prima di #4585 azzerava anche
    /// le quattro colonne che <c>MapToEntity</c> non copiava (<c>has_knowledge_base</c>,
    /// <c>is_rag_public</c>, <c>BggRawData</c>, <c>test_run_id</c>): il voto è pubblico e anonimo, e
    /// chiunque poteva togliere il gioco dal filtro «AI-ready».
    /// <para>
    /// Il predicato sul gioco tiene il 404 per i giochi cancellati: la FAQ non ha un filtro suo, e senza
    /// accedere a <c>SharedGame</c> il filtro globale del gioco non entra nella query.
    /// </para>
    /// <para>
    /// Il conteggio si rilegge dopo l'UPDATE: sono due istruzioni, ma l'incremento è atomico. Un voto
    /// arrivato in mezzo è già nel conteggio che torna, e non si perde nulla.
    /// </para>
    /// </remarks>
    public async Task<FaqUpvoteResult?> IncrementFaqUpvoteAsync(Guid faqId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var updated = await DbContext.Set<GameFaqEntity>()
            .Where(f => f.Id == faqId && !f.SharedGame.IsDeleted)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.UpvoteCount, f => f.UpvoteCount + 1)
                      .SetProperty(f => f.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            return null;
        }

        return await DbContext.Set<GameFaqEntity>()
            .AsNoTracking()
            .Where(f => f.Id == faqId)
            .Select(f => new FaqUpvoteResult(f.SharedGameId, f.UpvoteCount))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="MapToDomain"/> più le FAQ che la query ha incluso, ricordate per
    /// <see cref="Update"/> (#4557).
    /// </summary>
    private static SharedGame MapToDomainWithFaqs(SharedGameEntity entity)
    {
        var sharedGame = MapToDomain(entity);

        var read = new Dictionary<Guid, GameFaqEntity>();
        foreach (var row in entity.Faqs)
        {
            sharedGame.LoadFaq(new GameFaq(
                row.Id,
                row.SharedGameId,
                row.Question,
                row.Answer,
                row.DisplayOrder,
                row.UpvoteCount,
                row.CreatedAt,
                row.UpdatedAt));
            read[row.Id] = CopyOf(row);
        }

        FaqsReadBy.AddOrUpdate(sharedGame, read);
        return sharedGame;
    }

    /// <remarks>
    /// #4543: l'unica lettura che carica le errata, perché modificarne o toglierne una richiede di
    /// averla nell'aggregato. Le carica tutte, non solo quella cercata: <see cref="Update"/> tocca
    /// solo le errata che la lettura ha visto, quindi le altre resterebbero comunque intatte, ma
    /// l'aggregato deve rappresentare il gioco intero. Le altre letture non le caricano: degli altri
    /// comandi nessuno ne ha bisogno, e <c>AddErrata</c> funziona anche senza (l'errata nuova diventa
    /// INSERT). Il dettaglio del gioco (<c>GetSharedGameByIdQueryHandler</c>) non ne ha bisogno: legge
    /// <c>game_errata</c> direttamente dal DbContext (#4565).
    /// </remarks>
    public async Task<SharedGame?> GetGameByErrataIdAsync(Guid errataId, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.SharedGames
            .AsNoTracking()
            .Include(g => g.Erratas)
            .Where(g => g.Erratas.Any(e => e.Id == errataId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomainWithErrata(gameEntity) : null;
    }

    /// <summary>
    /// <see cref="MapToDomain"/> più le errata che la query ha incluso, ricordate per
    /// <see cref="Update"/> (#4543).
    /// </summary>
    private static SharedGame MapToDomainWithErrata(SharedGameEntity entity)
    {
        var sharedGame = MapToDomain(entity);

        var read = new Dictionary<Guid, GameErrataEntity>();
        foreach (var row in entity.Erratas)
        {
            sharedGame.LoadErrata(new GameErrata(
                row.Id,
                row.SharedGameId,
                row.Description,
                row.PageReference,
                row.PublishedDate,
                row.CreatedAt));
            read[row.Id] = CopyOf(row);
        }

        ErrataReadBy.AddOrUpdate(sharedGame, read);
        return sharedGame;
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
