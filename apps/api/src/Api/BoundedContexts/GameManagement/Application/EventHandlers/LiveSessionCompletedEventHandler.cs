using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
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

    public LiveSessionCompletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<LiveSessionCompletedEventHandler> logger,
        TimeProvider timeProvider,
        IPlayRecordRepository playRecordRepository)
        : base(dbContext, logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _playRecordRepository = playRecordRepository ?? throw new ArgumentNullException(nameof(playRecordRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task HandleEventAsync(
        LiveSessionCompletedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        try
        {
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
            // Log at Information — legitimate replays are not failures.
            Logger.LogInformation(
                ex,
                "Skipping duplicate PlayRecord for LiveSession {SessionId}: SourceEventId={SourceEventId} (already recorded)",
                domainEvent.SessionId, domainEvent.EventId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // PlayRecord generation failure must not block session completion.
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to generate PlayRecord from completed LiveSession {SessionId}",
                domainEvent.SessionId);
        }
#pragma warning restore CA1031
    }

    private PlayRecord CreatePlayRecordFromEvent(LiveSessionCompletedEvent domainEvent)
    {
        var scoringConfig = ScoringConfigOf(domainEvent);

        // Issue #1938 / CF-2: propagate EventId so the resulting PlayRecord row carries the
        // source event id and the UNIQUE partial index blocks duplicate inserts on retry/replay.
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
                sourceEventId: domainEvent.EventId,
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
            sourceEventId: domainEvent.EventId);
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
