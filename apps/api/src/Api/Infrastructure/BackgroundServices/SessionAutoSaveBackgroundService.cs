using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that auto-saves all in-progress live game sessions every 10 minutes.
/// Creates a <see cref="PauseSnapshot"/> with <c>IsAutoSave = true</c> for each active session
/// so that players can recover session state after an unexpected disconnection or server restart.
///
/// Auto-save snapshots are separate from user-initiated saves and are deleted on resume
/// via <see cref="IPauseSnapshotRepository.DeleteAutoSavesBySessionIdAsync"/>.
///
/// Game Night Improvvisata — E4: Session auto-save.
/// </summary>
internal sealed class SessionAutoSaveBackgroundService : BackgroundService
{
    /// <summary>
    /// Well-known system user ID used as <c>SavedByUserId</c> for auto-save snapshots.
    /// This sentinel value allows distinguishing system-generated snapshots from user-initiated ones.
    /// </summary>
    internal static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionAutoSaveBackgroundService> _logger;

    public SessionAutoSaveBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionAutoSaveBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SessionAutoSaveBackgroundService started. Auto-save interval: {Interval} minutes",
            Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);

#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service from crashing the host process.
            // Individual session errors are handled inside AutoSaveActiveSessionsAsync.
            try
            {
                await AutoSaveActiveSessionsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error during auto-save cycle");
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("SessionAutoSaveBackgroundService stopped");
    }

    private async Task AutoSaveActiveSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IPauseSnapshotRepository>();

        var activeSessions = await sessionRepo
            .GetAllActiveAsync(ct)
            .ConfigureAwait(false);

        if (activeSessions.Count == 0)
        {
            _logger.LogDebug("Auto-save: no active sessions found");
            return;
        }

        _logger.LogInformation(
            "Auto-save starting for {Count} active session(s)",
            activeSessions.Count);

        foreach (var session in activeSessions)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Failure for one session must not block auto-save for others.
            try
            {
                var playerScores = session.Players
                    .Where(p => p.IsActive)
                    .Select(p => new PlayerScoreSnapshot(
                        PlayerName: p.DisplayName,
                        Score: p.TotalScore))
                    .ToList();

                var snapshot = PauseSnapshot.Create(
                    liveGameSessionId: session.Id,
                    currentTurn: session.CurrentTurnIndex,
                    currentPhase: session.GetCurrentPhaseName(),
                    playerScores: playerScores,
                    savedByUserId: SystemUserId,
                    isAutoSave: true,
                    gameStateJson: session.GameState?.RootElement.GetRawText());

                await snapshotRepo.AddAsync(snapshot, ct).ConfigureAwait(false);

                // PauseSnapshotRepository.AddAsync stages the entity; call SaveChanges here
                // because the repo does not own the UoW in background service context.
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Auto-saved session {SessionId} (SnapshotId={SnapshotId}, Turn={Turn})",
                    session.Id, snapshot.Id, session.CurrentTurnIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Auto-save failed for session {SessionId}. Continuing with remaining sessions",
                    session.Id);
            }
#pragma warning restore CA1031
        }
    }
}
