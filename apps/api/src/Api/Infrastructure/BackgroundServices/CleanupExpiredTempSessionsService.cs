using Api.Services;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// R6 — periodic cleanup of expired / used temp 2FA sessions. Each call to
/// <see cref="ITempSessionService.CleanupExpiredSessionsAsync"/> removes:
///   - rows whose <c>ExpiresAt</c> is in the past, and
///   - rows whose <c>IsUsed</c> flag was set more than the audit retention
///     window ago (configured inside TempSessionService — currently 1 hour).
///
/// Without this background service the temp_sessions table grows monotonically
/// because the table is only ever appended to during 2FA verification (the
/// verify path flips IsUsed but doesn't delete). Over time the table becomes
/// hot for index maintenance and slows the per-attempt query path of C6.
///
/// The interval is intentionally aggressive (every 5 minutes) because temp
/// sessions are short-lived (5-minute TTL) and the cleanup query is cheap —
/// keeping the table small benefits the C6 atomic-update path more than
/// running cleanup once an hour would.
/// </summary>
internal sealed class CleanupExpiredTempSessionsService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CleanupExpiredTempSessionsService> _logger;

    public CleanupExpiredTempSessionsService(
        IServiceScopeFactory scopeFactory,
        ILogger<CleanupExpiredTempSessionsService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CleanupExpiredTempSessionsService started. Interval: {Interval} minute(s)",
            Interval.TotalMinutes);

        // Run once immediately so a freshly-deployed instance doesn't have to
        // wait the full interval to clear a backlog.
        await CleanupOnceAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Host shutdown — exit the loop cleanly.
                return;
            }

            await CleanupOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        // BACKGROUND SERVICE BOUNDARY: a transient error in cleanup
        // (network blip, DB restart) must NOT crash the host. Log and
        // continue — the next tick retries.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tempSessions = scope.ServiceProvider.GetRequiredService<ITempSessionService>();
            await tempSessions.CleanupExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to clean up expired temp 2FA sessions");
        }
#pragma warning restore CA1031
    }
}
