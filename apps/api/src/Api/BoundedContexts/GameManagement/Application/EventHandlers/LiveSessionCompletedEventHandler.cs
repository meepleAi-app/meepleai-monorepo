using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Observability;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Event handler for LiveSessionCompletedEvent.
/// Issue #4748: Generates a PlayRecord from a completed live game session.
/// Uses snapshot data carried by the domain event to avoid querying back.
/// Persists via IPlayRecordRepository (domain→persistence entity mapping).
/// </summary>
internal sealed class LiveSessionCompletedEventHandler : DomainEventHandlerBase<LiveSessionCompletedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IPlayRecordRepository _playRecordRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IDomainEventCollector _eventCollector;

    public LiveSessionCompletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<LiveSessionCompletedEventHandler> logger,
        TimeProvider timeProvider,
        IPlayRecordRepository playRecordRepository,
        IDomainEventCollector eventCollector)
        : base(dbContext, logger)
    {
        _eventCollector = eventCollector ?? throw new ArgumentNullException(nameof(eventCollector));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _playRecordRepository = playRecordRepository ?? throw new ArgumentNullException(nameof(playRecordRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task HandleEventAsync(
        LiveSessionCompletedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // The context (and the event collector next to it) is SHARED with whoever delivers the event: the outbox
        // processor saves it once more at the end of its batch. What this handler stages and fails to save must
        // not stay staged, so remember what the caller had there, to forget only the rest.
        var stagedBefore = StagedInserts();
        var queuedBefore = _eventCollector.PeekEvents() ?? Array.Empty<IDomainEvent>();

        try
        {
            // #4753: the record is keyed by the session, so a second delivery meets the unique index. The common
            // case is answered with a read, before anything is staged. IgnoreQueryFilters: the index also counts
            // the soft-deleted rows, and a replay must not bring back a record the user deleted.
            var alreadyRecorded = await _dbContext.PlayRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(r => r.SourceEventId == domainEvent.SessionId, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyRecorded)
            {
                Logger.LogInformation(
                    "Skipping PlayRecord for LiveSession {SessionId}: already recorded (EventId={EventId})",
                    domainEvent.SessionId, domainEvent.EventId);
                return;
            }

            var playRecord = CreatePlayRecordFromEvent(domainEvent);

            // Add players from session snapshot
            foreach (var playerSnapshot in domainEvent.Players)
            {
                playRecord.AddPlayer(playerSnapshot.UserId, playerSnapshot.DisplayName, _timeProvider);

                var recordPlayer = playRecord.Players.First(p =>
                    string.Equals(p.DisplayName, playerSnapshot.DisplayName, StringComparison.Ordinal));

                // #4156: one score per dimension, the total over the rounds. A play record keeps a
                // single value per player and dimension, so recording each round of the live session
                // replaced the previous one and the record carried the last round, not the total.
                var totals = domainEvent.Scores
                    .Where(s => s.PlayerId == playerSnapshot.PlayerId)
                    .GroupBy(s => s.Dimension, StringComparer.OrdinalIgnoreCase);

                foreach (var dimension in totals)
                {
                    var unit = dimension.Select(s => s.Unit).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
                    var recordScore = new RecordScore(dimension.First().Dimension, dimension.Sum(s => s.Value), unit);
                    playRecord.RecordScore(recordPlayer.Id, recordScore, _timeProvider);
                }
            }

            // Start and complete the record with session duration
            playRecord.Start(_timeProvider);

            var duration = domainEvent.StartedAt.HasValue
                ? domainEvent.CompletedAt - domainEvent.StartedAt.Value
                : (TimeSpan?)null;

            playRecord.Complete(duration, _timeProvider);

            if (domainEvent.Notes != null)
            {
                playRecord.UpdateDetails(notes: domainEvent.Notes, timeProvider: _timeProvider);
            }

            // Persist via repository (maps domain PlayRecord → PlayRecordEntity for EF Core)
            await _playRecordRepository.AddAsync(playRecord, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Generated PlayRecord {PlayRecordId} from completed LiveSession {SessionId}",
                playRecord.Id,
                domainEvent.SessionId);
        }
        catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
        {
            // CF-2 / #1938: outbox replay (#1535 at-least-once delivery) for a session
            // already mapped to a PlayRecord. The partial UNIQUE index on
            // play_records.source_event_id blocked the duplicate insert at the DB.
            // #4753: since the key is the session this branch is live (two deliveries at the same
            // instant both pass the read above). The rows that were staged stay Added after a failed
            // save, and the next SaveChangesAsync on this context would send them again.
            // Log at Information — legitimate replays are not failures.
            ForgetWhatWasStaged(stagedBefore, queuedBefore);
            Logger.LogInformation(
                ex,
                "Skipping duplicate PlayRecord for LiveSession {SessionId}: EventId={EventId} (already recorded)",
                domainEvent.SessionId, domainEvent.EventId);
        }
        catch (OperationCanceledException)
        {
            // A cancelled call (the outbox processor shutting down, a request aborted in Hybrid) is not a failure to
            // swallow or to count: the context is put back as the caller left it, and the cancellation goes on.
            ForgetWhatWasStaged(stagedBefore, queuedBefore);
            throw;
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        catch (Exception ex) when (PersistenceFailure.IsInfrastructure(ex))
        {
            // The database is slow or gone (#4839, option B): the record is not written, and swallowing that would lose
            // it for good. The failure goes up, and the outbox retries the row once the database is back (in
            // OutboxOnly that is the retry; in Hybrid the in-process dispatcher counts it and the outbox row is
            // delivered on its own). The retry runs every handler of the event again. That is safe for the ones that
            // count: the statistics skip a SESSION they already counted, whatever event came in between (#4839 option C;
            // bounded to the last sessions a memory counted, see ProcessedSessions), the proposal expiry only touches
            // proposals still pending and this handler's own read finds the record. It is not free for the others:
            // the stream forwarder sends session:endgame again and the audit handler writes another row.
            // The context is put back first, as for a cancellation: the failed insert must not stay staged for the
            // outbox processor's final save.
            ForgetWhatWasStaged(stagedBefore, queuedBefore);
            Logger.LogWarning(
                ex,
                "PlayRecord for LiveSession {SessionId} not written, the infrastructure failed: the outbox retries the event",
                domainEvent.SessionId);
            throw;
        }
#pragma warning restore S2139
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // A failure of the DATA must not block the OTHER handlers of the event.
        catch (Exception ex)
        {
            // Swallowed on purpose (#4839): a failure that is not the infrastructure's (a validation of the record, a
            // value the column refuses) would fail the same way at every retry. MediatR runs the handlers of an event
            // in sequence (ForeachAwaitPublisher, no custom NotificationPublisher), so an exception here would stop
            // the ones after it (stream forwarder, proposal expiry, player statistics, audit) on every retry and park
            // the row in Failed. That holds only if the failed insert is not left staged for the caller's next save
            // to throw again, and it must not be silent: it is counted where every other handler failure is counted.
            ForgetWhatWasStaged(stagedBefore, queuedBefore);
            MeepleAiMetrics.DomainEventDispatchFailures.Add(
                1,
                new KeyValuePair<string, object?>("event_type", EventTypeRegistry.ResolveOrFullName(domainEvent)),
                new KeyValuePair<string, object?>("handler_name", nameof(LiveSessionCompletedEventHandler)));
            Logger.LogError(ex,
                "Failed to generate PlayRecord from completed LiveSession {SessionId}",
                domainEvent.SessionId);
        }
#pragma warning restore CA1031
    }

    private HashSet<object> StagedInserts() =>
        _dbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Puts the shared context and the event collector back as the caller left them. The rows go back to
    /// <c>Detached</c>; and the collector is restored too, because the repository queued the record's events
    /// when it was asked to add it, and they stay queued after a failed save: the next save would write them
    /// (and an outbox row each) for a record that was never written.
    /// </summary>
    private void ForgetWhatWasStaged(HashSet<object> stagedBefore, IReadOnlyList<IDomainEvent> queuedBefore)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added && !stagedBefore.Contains(e.Entity))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        _eventCollector.Clear();
        foreach (var queued in queuedBefore)
        {
            _eventCollector.Collect(queued);
        }
    }

    private PlayRecord CreatePlayRecordFromEvent(LiveSessionCompletedEvent domainEvent)
    {
        var scoringConfig = ScoringConfigOf(domainEvent);

        // Issue #1938 / CF-2: the PlayRecord row carries a source key and the UNIQUE partial index on
        // play_records.source_event_id blocks a second insert for it. #4753: the key is the SESSION, not
        // domainEvent.EventId. EventId is a Guid.NewGuid() of each event INSTANCE and does not survive the
        // outbox (the event is rebuilt through its constructor), so the same completion delivered twice —
        // in-process and from the outbox, or an outbox replay — carried two keys and produced two records.
        // A live session completes once (Complete() refuses a session that is not in progress or paused).
        var sourceKey = domainEvent.SessionId;
        if (domainEvent.GameId.HasValue)
        {
            return PlayRecord.CreateWithGame(
                id: Guid.NewGuid(),
                gameId: domainEvent.GameId.Value,
                gameName: domainEvent.GameName,
                userId: domainEvent.CreatedByUserId,
                sessionDate: domainEvent.SessionDate,
                visibility: domainEvent.Visibility,
                timeProvider: _timeProvider,
                groupId: domainEvent.GroupId,
                sourceEventId: sourceKey,
                scoringConfig: scoringConfig);
        }

        return PlayRecord.CreateFreeForm(
            id: Guid.NewGuid(),
            gameName: domainEvent.GameName,
            userId: domainEvent.CreatedByUserId,
            sessionDate: domainEvent.SessionDate,
            visibility: domainEvent.Visibility,
            scoringConfig: scoringConfig,
            timeProvider: _timeProvider,
            groupId: domainEvent.GroupId,
            sourceEventId: sourceKey);
    }

    /// <summary>
    /// The scoring config of the play record: the dimensions the live session scored, with their
    /// units (#4156). The default config enables only "points", so a live session scored on any
    /// other dimension — «Punti» is the wizard's default — made recording its scores throw, and the
    /// handler swallowed it: the play record was never written. Without scores, the default.
    /// </summary>
    private static SessionScoringConfig ScoringConfigOf(LiveSessionCompletedEvent domainEvent)
    {
        var dimensions = domainEvent.Scores
            .GroupBy(s => s.Dimension, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (dimensions.Count == 0)
            return SessionScoringConfig.CreateDefault();

        var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dimension in dimensions)
        {
            var unit = dimension.Select(s => s.Unit).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (unit != null)
                units[dimension.First().Dimension] = unit;
        }

        return new SessionScoringConfig(dimensions.Select(d => d.First().Dimension), units);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(LiveSessionCompletedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["CompletedAt"] = domainEvent.CompletedAt,
            ["TotalTurns"] = domainEvent.TotalTurns,
            ["PlayerCount"] = domainEvent.Players.Count,
            ["Action"] = "LiveSessionCompleted_PlayRecordGenerated"
        };
    }
}
