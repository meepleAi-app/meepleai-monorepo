using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using MediatR;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that periodically refreshes the active-impersonation count into the
/// singleton <see cref="IImpersonationHealthTracker"/>, which the
/// <c>meepleai.security.impersonation.active.count</c> ObservableGauge reads on collection.
///
/// A periodic real query (default every 30s) is the source of truth — robust against process
/// restarts and the three lifecycle exits (self-end, superadmin revoke, middleware auto-expiry).
/// The query is cheap (partial index, small active set). SP5 Admin Security S2 — Task 7.
/// </summary>
internal sealed class ImpersonationMetricsRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IImpersonationHealthTracker _tracker;
    private readonly ILogger<ImpersonationMetricsRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(30);

    public ImpersonationMetricsRefreshService(
        IServiceScopeFactory scopeFactory,
        IImpersonationHealthTracker tracker,
        ILogger<ImpersonationMetricsRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Resilience: a failed refresh must not stop the host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "ImpersonationMetricsRefreshService refresh failed; will retry on next tick");
            }

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
        }
    }

    /// <summary>
    /// Queries the active impersonation count and pushes it into the tracker. Exposed for testing.
    /// </summary>
    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var active = await mediator.Send(new GetActiveImpersonationsQuery(), cancellationToken).ConfigureAwait(false);
        _tracker.SetActiveCount(active.Count);
    }
}
