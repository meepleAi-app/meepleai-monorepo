using System.Runtime.CompilerServices;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of UserLibrary repository.
/// Maps between domain UserLibraryEntry entity and UserLibraryEntryEntity persistence model.
/// </summary>
internal class UserLibraryRepository : RepositoryBase, IUserLibraryRepository
{
    // Issue #4138: the logger only served the warning on an undeserializable per-game agent
    // configuration, which this repository no longer reads.
    public UserLibraryRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<UserLibraryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        // Private-game entries (SharedGameId == null) cannot be mapped to UserLibraryEntry domain model
        return entity != null && entity.SharedGameId != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<UserLibraryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.SharedGameId != null) // exclude private-game entries
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<UserLibraryEntry?> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.UserLibraryEntries
            .AsNoTracking()
            // #4505: le etichette assegnate, perché AddLabel e RemoveLabel lavorano su questa lettura:
            // senza, il doppione non era un conflitto e la rimozione rispondeva 404. Sessioni e
            // checklist no: nessun handler che legge da qui le cambia, e le partite di una voce
            // possono essere molte. UpdateAsync non tocca ciò che la lettura non ha caricato.
            .Include(e => e.Labels)
            .FirstOrDefaultAsync(e => e.UserId == userId && (e.SharedGameId == gameId || e.PrivateGameId == gameId), cancellationToken)
            .ConfigureAwait(false);

        // Private-game entries (SharedGameId == null) cannot be mapped to the catalog UserLibraryEntry domain model
        return entity != null && entity.SharedGameId != null ? MapToDomain(entity, ChildrenRead.Labels) : null;
    }

    public async Task<(IReadOnlyList<UserLibraryEntry> Entries, int Total)> GetUserLibraryPaginatedAsync(
        Guid userId,
        string? search,
        bool? favoritesOnly,
        string[]? stateFilter,
        bool? hasKb,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters: the same bounds the handler reports (#4187).
        page = UserLibraryPaging.NormalizePage(page);
        pageSize = UserLibraryPaging.NormalizePageSize(pageSize);

        var query = DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.SharedGame)
            // Only catalog (shared) games — private game entries have null SharedGameId
            // and are managed separately via the PrivateGames endpoints.
            .Where(e => e.UserId == userId && e.SharedGameId != null);

        // Apply search filter (by game title)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.SharedGame != null && EF.Functions.ILike(e.SharedGame.Title, $"%{search}%"));
        }

        // Apply favorites filter
        if (favoritesOnly == true)
        {
            query = query.Where(e => e.IsFavorite);
        }

        // Apply state filter (Issue #2866)
        if (stateFilter is { Length: > 0 })
        {
            var stateValues = stateFilter
                .Select(s => Enum.TryParse<GameStateType>(s, true, out var state) ? (int?)state : null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToArray();

            if (stateValues.Length > 0)
            {
                query = query.Where(e => stateValues.Contains(e.CurrentState));
            }
        }

        // KB filter (#4227): the same rule as HasKb in GetUserLibraryQueryHandler, applied in SQL so
        // the total and the pages cover the whole library, not one page filtered by the client.
        if (hasKb.HasValue)
        {
            var completed = LibraryKbStates.CompletedLowerCase;
            var wanted = hasKb.Value;
#pragma warning disable CA1304, CA1311, MA0011 // p.ProcessingState.ToLower() is EF-translated to SQL lower(), never run in-process
            query = query.Where(e => DbContext.PdfDocuments.Any(p =>
                p.SharedGameId == e.SharedGameId
                && p.PrivateGameId == null
                && completed.Contains(p.ProcessingState.ToLower())) == wanted);
#pragma warning restore CA1304, CA1311, MA0011
        }

        // Get total count before pagination
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply sorting
        query = ApplySorting(query, sortBy, descending);

        // Apply pagination
        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var entries = entities.Select(MapToDomain).ToList();

        return (entries, total);
    }

    public async Task<int> GetUserLibraryCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.SharedGameId != null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetFavoriteCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.SharedGameId != null && e.IsFavorite, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsGameInLibraryAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && (e.SharedGameId == gameId || e.PrivateGameId == gameId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsCustomCoverKeyInUseAsync(string customCoverR2Key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customCoverR2Key);

        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(e => e.CustomCoverR2Key == customCoverR2Key, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(DateTime? Oldest, DateTime? Newest)> GetLibraryDateRangeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Project Min/Max in the database instead of materializing all dates into memory
        var result = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.SharedGameId != null) // exclude private-game entries for consistency with count
            .GroupBy(_ => 1)
            .Select(g => new { Oldest = g.Min(e => (DateTime?)e.AddedAt), Newest = g.Max(e => (DateTime?)e.AddedAt) })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
            return (null, null);

        return (result.Oldest, result.Newest);
    }

    public async Task<UserLibraryEntry?> GetUserGameWithStatsAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.Sessions.OrderByDescending(s => s.PlayedAt))
            .Include(e => e.Checklist.OrderBy(c => c.DisplayOrder))
            .Include(e => e.Labels) // Issue #4259: Include labels for accurate counts
            .Include(e => e.PdfDocument)
            .FirstOrDefaultAsync(e => e.UserId == userId && (e.SharedGameId == gameId || e.PrivateGameId == gameId), cancellationToken)
            .ConfigureAwait(false);

        // Private-game entries (SharedGameId == null) cannot be mapped to the catalog domain model
        return entity != null && entity.SharedGameId != null ? MapToDomain(entity, ChildrenRead.All) : null;
    }

    public async Task<IReadOnlyList<UserLibraryEntry>> GetUserGamesAsync(
        Guid userId,
        GameStateType? state = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.SharedGame)
            .Where(e => e.UserId == userId && e.SharedGameId != null); // exclude private-game entries

        if (state.HasValue)
        {
            query = query.Where(e => e.CurrentState == (int)state.Value);
        }

        var entities = await query
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetPrivatePdfCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.PrivatePdfId != null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Dictionary<GameStateType, int>> GetStateCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var stateCounts = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.SharedGameId != null)
            .GroupBy(e => e.CurrentState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return stateCounts.ToDictionary(
            x => (GameStateType)x.State,
            x => x.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserLibraryEntry>> GetRecentlyPlayedAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Get entries with sessions, ordered by most recent session first
        var entities = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.SharedGame)
            .Include(e => e.Sessions)
            .Where(e => e.UserId == userId && e.SharedGameId != null && e.Sessions.Any()) // exclude private-game entries
            .OrderByDescending(e => e.Sessions.Max(s => s.PlayedAt))
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserLibraryEntry>> GetUnplayedGamesAsync(
        Guid userId,
        int daysSince,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSince);

        // Get entries with no sessions OR last session older than cutoffDate
        var entities = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.SharedGame)
            .Include(e => e.Sessions)
            .Where(e => e.UserId == userId && e.SharedGameId != null && // exclude private-game entries
                        (!e.Sessions.Any() || e.Sessions.Max(s => s.PlayedAt) < cutoffDate))
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        await DbContext.UserLibraryEntries.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddForPrivateGameAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);

        // Fix FK mapping: domain GameId routes to SharedGameId via backward-compat setter,
        // but for private games we need PrivateGameId set instead.
        entity.PrivateGameId = entity.SharedGameId;
        entity.SharedGameId = null;

        await DbContext.UserLibraryEntries.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        var sessions = entity.Sessions.ToList();
        var checklist = entity.Checklist.ToList();
        var labels = entity.Labels.ToList();

        // #4505: Update() sulla sola radice. Sul grafo intero marcava Modified anche i figli nuovi,
        // che hanno già l'id: un UPDATE di una riga che non esiste, cioè
        // DbUpdateConcurrencyException. I figli passano da ReconcileChildren.
        entity.Sessions.Clear();
        entity.Checklist.Clear();
        entity.Labels.Clear();

        DetachSavedCopy(entity.Id);
        DbContext.UserLibraryEntries.Update(entity);

        if (!ChildrenReadBy.TryGetValue(entry, out var read))
        {
            // Ogni lettura di questo repository registra l'aggregato che produce: uno che manca non
            // viene da una lettura. Con dei figli non si può sapere quali siano già salvati, e
            // trattarli tutti come nuovi darebbe INSERT di righe che magari esistono.
            if (sessions.Count + checklist.Count + labels.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Issue #4505: library entry {entry.Id} was not produced by a read of this repository "
                    + "and carries child rows, so their saved state is unknown. Read it with "
                    + "GetByUserAndGameAsync or GetUserGameWithStatsAsync before updating it.");
            }

            read = StoredChildren.NothingRead;
        }

        ReconcileChildren(sessions, read.Sessions, s => s.Id, SessionColumnsOutsideTheDomain);
        ReconcileChildren(checklist, read.Checklist, c => c.Id, []);
        ReconcileChildren(labels, read.Labels, l => l.Id, []);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Le collezioni figlie che una lettura ha messo nell'aggregato (#4505).
    /// </summary>
    [Flags]
    private enum ChildrenRead
    {
        None = 0,
        Sessions = 1,
        Checklist = 2,
        Labels = 4,
        All = Sessions | Checklist | Labels,
    }

    /// <summary>
    /// Le righe figlie come la lettura le ha trovate, per id. <c>null</c> per una collezione che
    /// la lettura non ha caricato.
    /// </summary>
    private sealed record StoredChildren(
        IReadOnlyDictionary<Guid, UserGameSessionEntity>? Sessions,
        IReadOnlyDictionary<Guid, UserGameChecklistEntity>? Checklist,
        IReadOnlyDictionary<Guid, UserGameLabelEntity>? Labels)
    {
        public static StoredChildren NothingRead { get; } = new(null, null, null);
    }

    /// <summary>
    /// Che cosa ha caricato la lettura che ha prodotto ciascun aggregato (#4505).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UpdateAsync"/> riceve solo l'aggregato, e l'aggregato da solo non dice se una sua
    /// collezione è vuota o non è stata letta: <see cref="GetByUserAndGameAsync"/>, la lettura di
    /// tutti gli handler di scrittura, non carica sessioni e checklist. Cancellare "ciò che è nel
    /// database e non nell'aggregato" cancellerebbe ogni partita a ogni cambio di stato. Si cancella
    /// quindi solo ciò che la lettura ha caricato e il dominio ha tolto: una riga che la lettura non
    /// ha visto (non caricata, o scritta dopo da fuori dell'aggregato) non si tocca.
    /// </para>
    /// <para>
    /// La chiave è l'istanza dell'aggregato, non il repository: per questo la tabella è statica, e
    /// un aggregato letto da un'istanza e salvato da un'altra resta corretto. Tiene gli aggregati
    /// per riferimento debole: la voce sparisce con l'aggregato.
    /// </para>
    /// <para>
    /// Ogni lettura registra l'aggregato che produce, anche senza figli. Uno che manca non viene da
    /// una lettura: se porta figli, <see cref="UpdateAsync"/> si ferma con un'eccezione.
    /// </para>
    /// </remarks>
    private static readonly ConditionalWeakTable<UserLibraryEntry, StoredChildren> ChildrenReadBy = new();

    /// <summary>
    /// Colonne di una partita che il dominio non porta, quindi non scrive mai (#4505):
    /// <c>test_run_id</c> lo mette solo il seme dei test E2E, all'inserimento.
    /// </summary>
    private static readonly string[] SessionColumnsOutsideTheDomain = [nameof(UserGameSessionEntity.TestRunId)];

    /// <summary>
    /// Porta nel change tracker una collezione figlia dell'aggregato (#4505).
    /// </summary>
    /// <remarks>
    /// Un figlio che la lettura non aveva è nato nel dominio: <c>Added</c>. Uno letto e ancora
    /// presente parte dai valori letti e riceve quelli del dominio, così EF scrive solo le colonne
    /// cambiate — nessuna, nessun UPDATE. Uno letto e tolto dal dominio è <c>Deleted</c>.
    /// <para>
    /// I figli non hanno un token di concorrenza. Una scrittura da una lettura superata fallisce
    /// comunque per intero, perché l'UPDATE della radice controlla <c>xmin</c> nella stessa
    /// transazione. Ma non sempre per quel motivo: EF inserisce i figli prima di aggiornare la
    /// radice, quindi un'etichetta assegnata due volte in concorrenza colpisce prima l'indice unico
    /// (23505). <c>AddLabelToGameCommandHandler</c> lo traduce in 409.
    /// </para>
    /// </remarks>
    private void ReconcileChildren<TChild>(
        IReadOnlyList<TChild> current,
        IReadOnlyDictionary<Guid, TChild>? stored,
        Func<TChild, Guid> idOf,
        IReadOnlyCollection<string> columnsOutsideTheDomain)
        where TChild : class
    {
        var currentIds = new HashSet<Guid>();
        foreach (var child in current)
        {
            var id = idOf(child);
            currentIds.Add(id);

            if (stored is null || !stored.TryGetValue(id, out var row))
            {
                DbContext.Entry(child).State = EntityState.Added;
                continue;
            }

            var tracked = TrackAsStored(row, id);
            tracked.CurrentValues.SetValues(child);
            foreach (var column in columnsOutsideTheDomain)
            {
                var property = tracked.Property(column);
                property.CurrentValue = property.OriginalValue;
                property.IsModified = false;
            }
        }

        if (stored is null)
        {
            return;
        }

        foreach (var (id, row) in stored)
        {
            if (!currentIds.Contains(id))
            {
                TrackAsStored(row, id).State = EntityState.Deleted;
            }
        }
    }

    /// <summary>
    /// La riga figlia nel change tracker, con i valori letti come originali. Se il contesto la
    /// traccia già usa quella: attaccarne una seconda con la stessa chiave fallirebbe.
    /// </summary>
    /// <remarks>
    /// Sulla rotta PATCH con <c>newState</c> (#4253) non succede: <see cref="DetachSavedCopy"/>
    /// stacca la copia salvata della radice, ed EF stacca con lei i figli che traccia (misurato).
    /// Il ramo serve a un figlio tracciato senza la sua radice, per esempio da una lettura tracciata
    /// nella stessa richiesta.
    /// </remarks>
    private EntityEntry<TChild> TrackAsStored<TChild>(TChild row, Guid id)
        where TChild : class
    {
        var tracked = DbContext.Set<TChild>().Local.FindEntry(id);
        if (tracked is not null)
        {
            return tracked;
        }

        // Una copia dei soli valori: la riga letta punta ancora, per navigazione, alla radice della
        // lettura, e attaccarla porterebbe con sé una seconda istanza della radice.
        var copy = (TChild)DbContext.Entry(row).CurrentValues.ToObject();
        var entry = DbContext.Entry(copy);
        entry.State = EntityState.Unchanged;
        return entry;
    }

    /// <summary>
    /// #4253: stacca la copia della stessa voce che questo contesto traccia ancora da una scrittura
    /// precedente, se quella scrittura è già salvata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UpdateAsync"/> costruisce ogni volta un'istanza nuova e la passa a
    /// <c>DbSet.Update</c>. Dopo <c>SaveChanges</c> quell'istanza resta tracciata, quindi una
    /// seconda scrittura sulla stessa voce nello stesso contesto — <c>PATCH
    /// /library/games/{id}</c> con <c>newState</c> manda due comandi nella stessa richiesta —
    /// falliva con <see cref="InvalidOperationException"/> («already being tracked»), cioè un 500.
    /// </para>
    /// <para>
    /// Una copia <see cref="EntityState.Unchanged"/> non porta nulla che il database non abbia già:
    /// staccarla è sicuro, e la scrittura nuova passa dallo stesso percorso di un contesto pulito,
    /// con il token <c>xmin</c> che l'aggregato ha letto. Una copia con modifiche pendenti invece
    /// resta dov'è: staccarla le perderebbe senza traccia, e il conflitto di EF è il segnale giusto.
    /// </para>
    /// </remarks>
    private void DetachSavedCopy(Guid entryId)
    {
        var tracked = DbContext.UserLibraryEntries.Local.FirstOrDefault(e => e.Id == entryId);
        if (tracked is null)
        {
            return;
        }

        var trackedEntry = DbContext.Entry(tracked);
        if (trackedEntry.State == EntityState.Unchanged)
        {
            trackedEntry.State = EntityState.Detached;
        }
    }

    public Task DeleteAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        DbContext.UserLibraryEntries.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<UserLibraryEntryEntity> ApplySorting(
        IQueryable<UserLibraryEntryEntity> query,
        string? sortBy,
        bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(e => e.SharedGame != null ? e.SharedGame.Title : "")
                : query.OrderBy(e => e.SharedGame != null ? e.SharedGame.Title : ""),
            "favorite" => descending
                ? query.OrderByDescending(e => e.IsFavorite).ThenByDescending(e => e.AddedAt)
                : query.OrderBy(e => e.IsFavorite).ThenByDescending(e => e.AddedAt),
            "addedat" or _ => descending
                ? query.OrderByDescending(e => e.AddedAt)
                : query.OrderBy(e => e.AddedAt),
        };
    }

    /// <summary>
    /// Maps persistence entity to domain entity, without child collections.
    /// </summary>
    private static UserLibraryEntry MapToDomain(UserLibraryEntryEntity entity) => MapToDomain(entity, ChildrenRead.None);

    /// <summary>
    /// Maps persistence entity to domain entity, with the child collections the query loaded.
    /// </summary>
    /// <param name="entity">The row, with <paramref name="read"/> included.</param>
    /// <param name="read">
    /// The collections the query included, and only those: a collection listed here without its
    /// <c>Include</c> would reach the aggregate empty, as if the entry had none.
    /// </param>
    private static UserLibraryEntry MapToDomain(UserLibraryEntryEntity entity, ChildrenRead read)
    {
        var entry = new UserLibraryEntry(entity.Id, entity.UserId, entity.GameId);

        // #3651/#3688: il token letto viaggia con l'aggregato perché UpdateAsync persiste un
        // grafo detached. Senza questa riga MapToPersistence produrrebbe Xmin = 0 e ogni
        // scrittura fallirebbe con DbUpdateConcurrencyException — su ogni update, non su una race.
        entry.SetXmin(entity.Xmin);

        // Set notes if present
        if (!string.IsNullOrWhiteSpace(entity.Notes))
        {
            entry.UpdateNotes(new LibraryNotes(entity.Notes));
        }

        // Set favorite status
        if (entity.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        // Reconstruct GameState from database. #4499: con la data di cambio salvata — le factory
        // (GameState.Owned(...) e le altre) la mettevano a "adesso", e ogni update la riscriveva.
        var stateType = (GameStateType)entity.CurrentState;
        var gameState = Enum.IsDefined(stateType)
            ? GameState.Reconstitute(stateType, entity.StateChangedAt, entity.StateNotes)
            : GameState.Nuovo();

        // Override CurrentState using reflection
        var currentStateProp = typeof(UserLibraryEntry).GetProperty("CurrentState");
        currentStateProp?.SetValue(entry, gameState);

        // Reconstruct GameStats from database
        var gameStats = GameStats.Create(
            timesPlayed: entity.TimesPlayed,
            lastPlayed: entity.LastPlayed,
            winRate: entity.WinRate,
            avgDuration: entity.AvgDuration,
            competitiveSessions: entity.CompetitiveSessions);

        var statsProp = typeof(UserLibraryEntry).GetProperty("Stats");
        statsProp?.SetValue(entry, gameStats);

        // Reconstruct custom PDF metadata
        if (!string.IsNullOrWhiteSpace(entity.CustomPdfUrl) &&
            entity.CustomPdfUploadedAt.HasValue &&
            entity.CustomPdfFileSizeBytes.HasValue &&
            !string.IsNullOrWhiteSpace(entity.CustomPdfOriginalFileName))
        {
            var pdfMetadata = CustomPdfMetadata.CreateWithTimestamp(
                url: entity.CustomPdfUrl,
                uploadedAt: entity.CustomPdfUploadedAt.Value,
                fileSizeBytes: entity.CustomPdfFileSizeBytes.Value,
                originalFileName: entity.CustomPdfOriginalFileName
            );
            entry.UploadCustomPdf(pdfMetadata);
        }

        // Override AddedAt from DB using reflection (same pattern as GameRepository)
        var addedAtProp = typeof(UserLibraryEntry).GetProperty("AddedAt");
        addedAtProp?.SetValue(entry, entity.AddedAt);

        // Set PrivatePdfId from DB using reflection (avoid raising domain event for existing data)
        if (entity.PrivatePdfId.HasValue)
        {
            var privatePdfIdProp = typeof(UserLibraryEntry).GetProperty("PrivatePdfId");
            privatePdfIdProp?.SetValue(entry, entity.PrivatePdfId.Value);
        }

        // Hydrate OwnershipDeclaredAt from DB using reflection (private setter)
        if (entity.OwnershipDeclaredAt.HasValue)
        {
            var ownershipDeclaredAtProp = typeof(UserLibraryEntry).GetProperty("OwnershipDeclaredAt");
            ownershipDeclaredAtProp?.SetValue(entry, entity.OwnershipDeclaredAt.Value);
        }

        // #4499: senza questa riga l'aggregato non conosceva la cover personalizzata — la
        // rimozione rispondeva 404 e il campo customCoverR2Key del dettaglio era sempre null.
        // Questa riga dà all'aggregato la chiave salvata; che il dettaglio la mostri subito
        // dipende anche dalla sua cache, che gli handler della cover invalidano.
        entry.SetCustomCoverR2Key(entity.CustomCoverR2Key);

        // #4499: colonna che il dominio trasporta e basta (vedi UserLibraryEntry.TestRunId).
        entry.SetTestRunId(entity.TestRunId);

        // Anche senza figli: UpdateAsync riconosce così un aggregato che nessuna lettura ha prodotto.
        RestoreChildren(entry, entity, read);

        // Clear domain events that were raised during construction
        // (we don't want to re-raise events for existing entities)
        entry.ClearDomainEvents();

        return entry;
    }

    /// <summary>
    /// Mette nell'aggregato le collezioni figlie lette, esattamente come sono salvate, e ricorda
    /// quali erano, per <see cref="UpdateAsync"/> (#4505).
    /// </summary>
    /// <remarks>
    /// Prima le partite e le voci di checklist nascevano dalle factory: date di creazione ad
    /// "adesso", e una voce completata con la data di modifica ad "adesso". Ogni update le
    /// riscriveva. Le etichette non arrivavano proprio, nemmeno dove la query le includeva.
    /// </remarks>
    private static void RestoreChildren(UserLibraryEntry entry, UserLibraryEntryEntity entity, ChildrenRead read)
    {
        var sessions = read.HasFlag(ChildrenRead.Sessions) ? entity.Sessions.ToList() : null;
        var checklist = read.HasFlag(ChildrenRead.Checklist) ? entity.Checklist.OrderBy(c => c.DisplayOrder).ToList() : null;
        var labels = read.HasFlag(ChildrenRead.Labels) ? entity.Labels.ToList() : null;

        entry.RestoreChildren(
            (sessions ?? []).Select(s => GameSession.Reconstitute(
                s.Id, s.UserLibraryEntryId, s.PlayedAt, s.DurationMinutes, s.DidWin, s.Players, s.Notes, s.CreatedAt, s.UpdatedAt)),
            (checklist ?? []).Select(c => GameChecklist.Reconstitute(
                c.Id, c.UserLibraryEntryId, c.Description, c.DisplayOrder, c.IsCompleted, c.AdditionalInfo, c.CreatedAt, c.UpdatedAt)),
            (labels ?? []).Select(l => UserGameLabel.Reconstitute(l.Id, l.UserLibraryEntryId, l.LabelId, l.AssignedAt)));

        ChildrenReadBy.AddOrUpdate(entry, read == ChildrenRead.None
            ? StoredChildren.NothingRead
            : new StoredChildren(
                sessions?.ToDictionary(s => s.Id),
                checklist?.ToDictionary(c => c.Id),
                labels?.ToDictionary(l => l.Id)));
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static UserLibraryEntryEntity MapToPersistence(UserLibraryEntry domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        // Access CompetitiveSessions via reflection (private property)
#pragma warning disable S3011 // Reflection needed for persistence mapping
        var competitiveSessionsProp = typeof(GameStats).GetProperty("CompetitiveSessions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var competitiveSessions = (int)(competitiveSessionsProp?.GetValue(domainEntity.Stats) ?? 0);
#pragma warning restore S3011

        var persistenceEntity = new UserLibraryEntryEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            AddedAt = domainEntity.AddedAt,
            // #3651/#3688: round-trip del concurrency token. Update() su un grafo detached usa i
            // valori correnti come original values, quindi senza questa riga la WHERE avrebbe
            // xmin = 0 — che non corrisponde a nessuna tupla viva — e ogni scrittura fallirebbe.
            Xmin = domainEntity.Xmin,
            Notes = domainEntity.Notes?.Value,
            IsFavorite = domainEntity.IsFavorite,
            CustomPdfUrl = domainEntity.CustomPdfMetadata?.Url,
            CustomPdfUploadedAt = domainEntity.CustomPdfMetadata?.UploadedAt,
            CustomPdfFileSizeBytes = domainEntity.CustomPdfMetadata?.FileSizeBytes,
            CustomPdfOriginalFileName = domainEntity.CustomPdfMetadata?.OriginalFileName,
            PrivatePdfId = domainEntity.PrivatePdfId,
            OwnershipDeclaredAt = domainEntity.OwnershipDeclaredAt,
            // #4499: omessa, ogni update scriveva custom_cover_r2_key = NULL, upload compreso.
            CustomCoverR2Key = domainEntity.CustomCoverR2Key,
            // #4499: omessa, ogni update azzerava test_run_id e la pulizia E2E perdeva la riga.
            TestRunId = domainEntity.TestRunId,

            // Game state
            CurrentState = (int)domainEntity.CurrentState.Value,
            StateChangedAt = domainEntity.CurrentState.ChangedAt,
            StateNotes = domainEntity.CurrentState.StateNotes,

            // Game statistics
            TimesPlayed = domainEntity.Stats.TimesPlayed,
            LastPlayed = domainEntity.Stats.LastPlayed,
            WinRate = domainEntity.Stats.WinRate,
            AvgDuration = domainEntity.Stats.AvgDuration,
            CompetitiveSessions = competitiveSessions
        };

        // Map Sessions to persistence entities
        foreach (var session in domainEntity.Sessions)
        {
            persistenceEntity.Sessions.Add(new UserGameSessionEntity
            {
                Id = session.Id,
                UserLibraryEntryId = domainEntity.Id,
                PlayedAt = session.PlayedAt,
                DurationMinutes = session.DurationMinutes,
                DidWin = session.DidWin,
                Players = session.Players,
                Notes = session.Notes,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            });
        }

        // Map Checklist to persistence entities
        foreach (var item in domainEntity.Checklist)
        {
            persistenceEntity.Checklist.Add(new UserGameChecklistEntity
            {
                Id = item.Id,
                UserLibraryEntryId = domainEntity.Id,
                Description = item.Description,
                DisplayOrder = item.DisplayOrder,
                IsCompleted = item.IsCompleted,
                AdditionalInfo = item.AdditionalInfo,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            });
        }

        // #4505: omesse, un'etichetta assegnata non arrivava mai al database.
        foreach (var label in domainEntity.Labels)
        {
            persistenceEntity.Labels.Add(new UserGameLabelEntity
            {
                Id = label.Id,
                UserLibraryEntryId = domainEntity.Id,
                LabelId = label.LabelId,
                AssignedAt = label.AssignedAt
            });
        }

        return persistenceEntity;
    }
}