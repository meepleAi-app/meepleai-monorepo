using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of PlayRecord repository.
/// Maps between domain PlayRecord entity and PlayRecordEntity persistence model.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal class PlayRecordRepository : RepositoryBase, IPlayRecordRepository
{
    public PlayRecordRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PlayRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<PlayRecord?> GetByIdWithPlayersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PlayRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .OrderByDescending(r => r.SessionDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<(IReadOnlyList<PlayRecord> Records, int Total)> GetUserHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .Where(r => r.CreatedByUserId == userId);

        if (gameId.HasValue)
        {
            query = query.Where(r => r.GameId == gameId.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(r => r.SessionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToList(), total);
    }

    public async Task<bool> CanUserViewAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken)
            .ConfigureAwait(false);

        if (record == null) return false;

        // Creator can always view
        if (record.CreatedByUserId == userId) return true;

        // Group members can view group records
        // Note: Group feature not yet implemented - will add group membership check in Issue #3891

        // Players can view their records
        return record.Players.Any(p => p.UserId == userId);
    }

    public async Task<bool> CanUserEditAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var creatorId = await DbContext.PlayRecords
            .AsNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => r.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return creatorId == userId;
    }

    public async Task AddAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CollectDomainEvents(record);

        var entity = MapToPersistence(record);
        await DbContext.PlayRecords.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CollectDomainEvents(record);

        var entity = MapToPersistence(record);

        // #4569: i punteggi escono dal grafo. Non hanno un id nel dominio, quindi MapToPersistence ne dà uno
        // nuovo a ogni chiamata: nel grafo diventavano tutti INSERT, e un punteggio già salvato violava
        // l'indice unico (giocatore, dimensione). Passano da ReconcileScores, che li confronta con le righe
        // lette per chiave naturale.
        var scores = new List<RecordScoreEntity>();
        foreach (var player in entity.Players)
        {
            scores.AddRange(player.Scores);
            player.Scores.Clear();
        }

        var scoresRead = ScoresReadFor(record, scores.Count);

        // Load existing entity to determine which child entities are new vs existing.
        // Using Update(entity) on a detached entity graph marks ALL children as Modified,
        // causing DbUpdateConcurrencyException for newly added children (0 rows affected).
        var existingPlayerIds = await DbContext.RecordPlayers
            .Where(p => p.PlayRecordId == record.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingPlayerIdSet = new HashSet<Guid>(existingPlayerIds);

        var existingPhotoIds = await DbContext.PlayRecordPhotos
            .Where(p => p.PlayRecordId == record.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingPhotoIdSet = new HashSet<Guid>(existingPhotoIds);

        // Attach root entity as Modified
        DbContext.PlayRecords.Update(entity);

        // Fix state for child entities: new entities should be Added, not Modified
        foreach (var player in entity.Players)
        {
            if (!existingPlayerIdSet.Contains(player.Id))
            {
                DbContext.Entry(player).State = EntityState.Added;
            }
        }

        foreach (var photo in entity.Photos)
        {
            if (!existingPhotoIdSet.Contains(photo.Id))
            {
                DbContext.Entry(photo).State = EntityState.Added;
            }
        }

        ReconcileScores(scores, scoresRead);
    }

    /// <summary>
    /// I punteggi che una lettura ha messo nell'aggregato, per (giocatore, dimensione), come le righe li avevano
    /// (#4569).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un <c>RecordScore</c> è un value object: non porta un id, e per il dominio l'identità di un punteggio è
    /// (giocatore, dimensione) — <c>RecordPlayer.RecordScore</c> sostituisce quello della stessa dimensione, senza
    /// badare alle maiuscole — che è anche la chiave dell'indice unico <c>IX_RecordScores_Player_Dimension_Unique</c>.
    /// <see cref="UpdateAsync"/> riceve solo l'aggregato, quindi per sapere quale riga è quale serve ciò che la
    /// lettura ha trovato. Ogni lettura di questo repository passa da <see cref="MapToDomain"/>, che registra
    /// l'aggregato che produce.
    /// </para>
    /// <para>
    /// Stesso schema di <c>UserLibraryRepository</c> (#4505) e <c>SharedGameRepository</c> (#4543): INSERT di ciò
    /// che la lettura non aveva, UPDATE delle sole colonne cambiate delle righe lette, DELETE delle righe lette
    /// e tolte dal dominio. Una riga che la lettura non ha visto non si tocca. La chiave è l'istanza
    /// dell'aggregato, tenuta per riferimento debole: la voce sparisce con lui, e un aggregato letto da
    /// un'istanza del repository e salvato da un'altra resta corretto.
    /// </para>
    /// </remarks>
    private static readonly ConditionalWeakTable<PlayRecord, IReadOnlyDictionary<ScoreKey, RecordScoreEntity>> ScoresReadBy = new();

    private static readonly IReadOnlyDictionary<ScoreKey, RecordScoreEntity> NoScoresRead =
        new Dictionary<ScoreKey, RecordScoreEntity>();

    /// <summary>
    /// (giocatore, dimensione) di un punteggio. La dimensione si confronta senza badare alle maiuscole, come fa
    /// <c>RecordPlayer</c>: "Punti" dopo "punti" è lo stesso punteggio, non un secondo.
    /// </summary>
    private readonly record struct ScoreKey(Guid PlayerId, string Dimension)
    {
        public bool Equals(ScoreKey other) =>
            PlayerId == other.PlayerId && string.Equals(Dimension, other.Dimension, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(PlayerId, StringComparer.OrdinalIgnoreCase.GetHashCode(Dimension));
    }

    /// <summary>
    /// Le righe di punteggio che la lettura di <paramref name="record"/> ha trovato (#4569).
    /// </summary>
    /// <remarks>
    /// Ogni lettura registra l'aggregato che produce: uno che manca non viene da una lettura. Con dei punteggi
    /// non si può sapere quali siano già salvati, e trattarli tutti come nuovi darebbe INSERT di righe che magari
    /// esistono, cioè il difetto di #4569. Si ferma prima di toccare il change tracker.
    /// </remarks>
    private static IReadOnlyDictionary<ScoreKey, RecordScoreEntity> ScoresReadFor(PlayRecord record, int scoreCount)
    {
        if (ScoresReadBy.TryGetValue(record, out var read))
        {
            return read;
        }

        if (scoreCount > 0)
        {
            throw new InvalidOperationException(
                $"Issue #4569: play record {record.Id} was not produced by a read of this repository and "
                + "carries scores, so their saved state is unknown. Read it with GetByIdAsync before updating it.");
        }

        return NoScoresRead;
    }

    /// <summary>
    /// Porta nel change tracker i punteggi dell'aggregato (#4569): <c>Added</c> quelli che la lettura non aveva;
    /// quelli letti e ancora presenti partono dai valori letti e ricevono quelli del dominio, così EF scrive solo
    /// le colonne cambiate (nessuna, nessun UPDATE) e la riga mantiene il suo id; quelli letti e tolti dal
    /// dominio diventano <c>Deleted</c>.
    /// </summary>
    /// <remarks>
    /// I punteggi non hanno un token di concorrenza: una scrittura da una lettura superata fallisce per intero
    /// perché l'UPDATE della partita, nella stessa transazione, controlla <c>xmin</c>. Quell'UPDATE c'è sempre
    /// perché <c>Update()</c> su un grafo staccato marca ogni colonna della radice: se un giorno la radice si
    /// salvasse solo quando cambia, i punteggi resterebbero senza protezione. Lo fissa
    /// <c>ConcurrentScoreWrites_TheSecondSaveFailsOnTheRecordXmin</c>. Due scrittori che inseriscono lo stesso
    /// (giocatore, dimensione) non arrivano a xmin: EF inserisce i figli prima di aggiornare la radice, e
    /// risponde l'indice unico (23505).
    /// </remarks>
    private void ReconcileScores(
        IReadOnlyList<RecordScoreEntity> current,
        IReadOnlyDictionary<ScoreKey, RecordScoreEntity> read)
    {
        var currentKeys = new HashSet<ScoreKey>();
        foreach (var score in current)
        {
            var key = new ScoreKey(score.RecordPlayerId, score.Dimension);
            currentKeys.Add(key);

            if (!read.TryGetValue(key, out var row))
            {
                DbContext.Entry(score).State = EntityState.Added;
                continue;
            }

            // L'id della riga letta, non quello che MapToPersistence ha dato: è la stessa riga.
            score.Id = row.Id;
            TrackAsRead(row).CurrentValues.SetValues(score);
        }

        foreach (var (key, row) in read)
        {
            if (!currentKeys.Contains(key))
            {
                TrackAsRead(row).State = EntityState.Deleted;
            }
        }
    }

    /// <summary>
    /// La riga di punteggio nel change tracker, con i valori letti come originali. Se il contesto la traccia
    /// già usa quella: attaccarne una seconda con la stessa chiave fallirebbe.
    /// </summary>
    private EntityEntry<RecordScoreEntity> TrackAsRead(RecordScoreEntity row)
    {
        var tracked = DbContext.Set<RecordScoreEntity>().Local.FindEntry(row.Id);
        if (tracked is not null)
        {
            return tracked;
        }

        // Una copia nuova a ogni salvataggio: la stessa istanza attaccata a due contesti porterebbe nel secondo
        // lo stato del primo.
        var entry = DbContext.Entry(CopyOf(row));
        entry.State = EntityState.Unchanged;
        return entry;
    }

    private static RecordScoreEntity CopyOf(RecordScoreEntity row) => new()
    {
        Id = row.Id,
        RecordPlayerId = row.RecordPlayerId,
        Dimension = row.Dimension,
        Value = row.Value,
        Unit = row.Unit,
    };

    public Task DeleteAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // #4569: la sola radice. Giocatori, punteggi e foto cadono col database (cascata sulle FK). Nel grafo,
        // i punteggi avevano un id inventato a ogni chiamata: un DELETE di una riga che non esiste, cioè
        // DbUpdateConcurrencyException.
        var entity = MapToPersistence(record);
        entity.Players.Clear();
        entity.Photos.Clear();
        DbContext.PlayRecords.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<PlayRecord?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shareToken);
        var entity = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players).ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.ShareToken == shareToken && r.IsShared, cancellationToken)
            .ConfigureAwait(false);
        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.PlayRecords
            .AsNoTracking()
            .AnyAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    /// <remarks>
    /// Passa da <see cref="PlayRecord.Reconstitute"/>, non dalle factory di creazione (#4552): quelle sollevano
    /// <c>PlayRecordCreatedEvent</c>, e il repository lo raccoglieva di nuovo al primo <see cref="UpdateAsync"/>
    /// dopo ogni lettura. I punteggi tornano con <see cref="PlayRecord.RestoreScore"/>, non con
    /// <see cref="PlayRecord.RecordScore"/>, che riportava <c>UpdatedAt</c> ad "adesso" e sollevava un evento per
    /// punteggio. Ogni colonna scritta da <see cref="MapToPersistence"/> torna qui (vedi
    /// <c>PlayRecordRepositoryTests</c>).
    /// </remarks>
    private static PlayRecord MapToDomain(PlayRecordEntity entity)
    {
        // Deserialize scoring config
        var scoringConfig = JsonSerializer.Deserialize<SessionScoringConfigDto>(entity.ScoringConfigJson)
            ?? throw new InvalidOperationException("Failed to deserialize ScoringConfigJson");

        var config = new SessionScoringConfig(
            scoringConfig.EnabledDimensions,
            scoringConfig.DimensionUnits);

        var record = PlayRecord.Reconstitute(
            id: entity.Id,
            gameId: entity.GameId,
            gameName: entity.GameName,
            createdByUserId: entity.CreatedByUserId,
            visibility: (PlayRecordVisibility)entity.Visibility,
            groupId: entity.GroupId,
            sessionDate: entity.SessionDate,
            startTime: entity.StartTime,
            endTime: entity.EndTime,
            duration: entity.Duration,
            status: (PlayRecordStatus)entity.Status,
            notes: entity.Notes,
            location: entity.Location,
            scoringConfig: config,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            isDeleted: entity.IsDeleted,
            deletedAt: entity.DeletedAt,
            sourceEventId: entity.SourceEventId,
            shareToken: entity.ShareToken,  // #2437-2
            isShared: entity.IsShared,
            xmin: entity.Xmin);  // round-trip for detached Update (ADR-060). #2436 PR-B / #2437-1.

        // Restore players and scores with original IDs (no domain events)
        // #4569: e le righe dei punteggi come sono salvate, per ReconcileScores — il dominio non ne porta l'id.
        var scoresRead = new Dictionary<ScoreKey, RecordScoreEntity>();
        foreach (var playerEntity in entity.Players)
        {
            record.RestorePlayer(playerEntity.Id, playerEntity.UserId, playerEntity.DisplayName);

            foreach (var scoreEntity in playerEntity.Scores)
            {
                record.RestoreScore(playerEntity.Id, new RecordScore(scoreEntity.Dimension, scoreEntity.Value, scoreEntity.Unit));
                scoresRead[new ScoreKey(playerEntity.Id, scoreEntity.Dimension)] = CopyOf(scoreEntity);
            }
        }

        ScoresReadBy.AddOrUpdate(record, scoresRead);

        // Restore photos with original IDs (no domain events) — #2436 PR-B
        foreach (var photoEntity in entity.Photos)
        {
            record.RestorePhoto(
                photoEntity.Id, photoEntity.BlobUrl, photoEntity.ThumbnailUrl,
                photoEntity.FileSizeBytes, photoEntity.Sha256Hash, photoEntity.OcrText,
                photoEntity.OcrConfidence, photoEntity.Caption, photoEntity.UploadedByUserId,
                photoEntity.UploadedAt);
        }

        return record;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static PlayRecordEntity MapToPersistence(PlayRecord record)
    {
        var entity = new PlayRecordEntity
        {
            Id = record.Id,
            GameId = record.GameId,
            GameName = record.GameName,
            CreatedByUserId = record.CreatedByUserId,
            Visibility = (int)record.Visibility,
            GroupId = record.GroupId,
            SessionDate = record.SessionDate,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            Duration = record.Duration,
            Status = (int)record.Status,
            Notes = record.Notes,
            Location = record.Location,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            SourceEventId = record.SourceEventId,
            IsDeleted = record.IsDeleted,
            DeletedAt = record.DeletedAt,
            Xmin = record.Xmin,  // round-trip for detached Update (ADR-060). #2436 PR-B / #2437-1.
            ShareToken = record.ShareToken,  // #2437-2
            IsShared = record.IsShared       // #2437-2
        };

        // Serialize scoring config
        var configDto = new SessionScoringConfigDto
        {
            EnabledDimensions = record.ScoringConfig.EnabledDimensions.ToList(),
            DimensionUnits = record.ScoringConfig.DimensionUnits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase)
        };
        entity.ScoringConfigJson = JsonSerializer.Serialize(configDto);

        // Map players
        foreach (var player in record.Players)
        {
            var playerEntity = new RecordPlayerEntity
            {
                Id = player.Id,
                PlayRecordId = record.Id,
                UserId = player.UserId,
                DisplayName = player.DisplayName
            };

            // Map scores
            foreach (var score in player.Scores)
            {
                var scoreEntity = new RecordScoreEntity
                {
                    Id = Guid.NewGuid(),
                    RecordPlayerId = player.Id,
                    Dimension = score.Dimension,
                    Value = score.Value,
                    Unit = score.Unit
                };
                playerEntity.Scores.Add(scoreEntity);
            }

            entity.Players.Add(playerEntity);
        }

        // Map photos — #2436 PR-B
        foreach (var photo in record.Photos)
        {
            entity.Photos.Add(new PlayRecordPhotoEntity
            {
                Id = photo.Id,
                PlayRecordId = record.Id,
                BlobUrl = photo.BlobUrl,
                ThumbnailUrl = photo.ThumbnailUrl,
                FileSizeBytes = photo.FileSizeBytes,
                Sha256Hash = photo.Sha256Hash,
                OcrText = photo.OcrText,
                OcrConfidence = photo.OcrConfidence,
                Caption = photo.Caption,
                UploadedByUserId = photo.UploadedByUserId,
                UploadedAt = photo.UploadedAt,
            });
        }

        return entity;
    }

    /// <summary>
    /// DTO for JSON serialization of SessionScoringConfig.
    /// </summary>
    private sealed class SessionScoringConfigDto
    {
        public List<string> EnabledDimensions { get; set; } = new();
        public Dictionary<string, string> DimensionUnits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
