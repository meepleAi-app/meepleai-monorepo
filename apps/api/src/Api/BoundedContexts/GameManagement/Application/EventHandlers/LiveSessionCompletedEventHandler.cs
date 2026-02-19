using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Event handler for LiveSessionCompletedEvent.
/// Issue #4748: Generates a PlayRecord from a completed live game session.
/// Uses snapshot data carried by the domain event to avoid querying back.
/// </summary>
internal sealed class LiveSessionCompletedEventHandler : DomainEventHandlerBase<LiveSessionCompletedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LiveSessionCompletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<LiveSessionCompletedEventHandler> logger,
        TimeProvider timeProvider)
        : base(dbContext, logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

                // Map round scores for this player
                var playerScores = domainEvent.Scores
                    .Where(s => s.PlayerId == playerSnapshot.PlayerId)
                    .ToList();

                var recordPlayer = playRecord.Players.First(p =>
                    string.Equals(p.DisplayName, playerSnapshot.DisplayName, StringComparison.Ordinal));

                foreach (var score in playerScores)
                {
                    var recordScore = new RecordScore(score.Dimension, score.Value, score.Unit);
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

            _dbContext.Add(playRecord);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Generated PlayRecord {PlayRecordId} from completed LiveSession {SessionId}",
                playRecord.Id,
                domainEvent.SessionId);
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
                groupId: domainEvent.GroupId);
        }

        return PlayRecord.CreateFreeForm(
            id: Guid.NewGuid(),
            gameName: domainEvent.GameName,
            userId: domainEvent.CreatedByUserId,
            sessionDate: domainEvent.SessionDate,
            visibility: domainEvent.Visibility,
            scoringConfig: SessionScoringConfig.CreateDefault(),
            timeProvider: _timeProvider,
            groupId: domainEvent.GroupId);
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
