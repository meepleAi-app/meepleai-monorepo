using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure.Configuration;
using Api.Infrastructure.Entities.Administration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically polls ASP.NET Core health checks and
/// manages service health state with hysteresis-based transitions.
/// Publishes <see cref="HealthStatusChangedEvent"/> on status changes and periodic reminders.
///
/// Issue #448: Infrastructure Health Monitoring — Epic Service Health Monitoring.
/// </summary>
internal sealed class InfrastructureHealthMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPublisher _publisher;
    private readonly HealthMonitorOptions _options;
    private readonly ILogger<InfrastructureHealthMonitorService> _logger;

    /// <summary>In-memory cache of health state per service, hydrated from DB on startup.</summary>
    private readonly Dictionary<string, ServiceHealthState> _states = new(StringComparer.OrdinalIgnoreCase);

    public InfrastructureHealthMonitorService(
        IServiceScopeFactory scopeFactory,
        IPublisher publisher,
        IOptions<HealthMonitorOptions> options,
        ILogger<InfrastructureHealthMonitorService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[HealthMonitor] Service started, waiting {Delay}s for initialization",
            _options.StartupDelaySeconds);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "[HealthMonitor] Cancelled during startup delay");
            return;
        }

        // Hydrate state from database
        await HydrateStateFromDatabaseAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[HealthMonitor] Polling loop started (interval: {Interval}s, states loaded: {Count})",
            _options.PollingIntervalSeconds, _states.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
#pragma warning disable CA1031 // Service boundary - must not crash host
            try
            {
                await PollHealthChecksAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[HealthMonitor] Error during health check poll");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[HealthMonitor] Service stopped");
    }

    private async Task HydrateStateFromDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var entities = await db.ServiceHealthStates
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                _states[entity.ServiceName] = new ServiceHealthState(
                    ServiceName: entity.ServiceName,
                    CurrentStatus: entity.CurrentStatus,
                    PreviousStatus: entity.PreviousStatus,
                    ConsecutiveFailures: entity.ConsecutiveFailures,
                    ConsecutiveSuccesses: entity.ConsecutiveSuccesses,
                    LastTransitionAt: entity.LastTransitionAt,
                    LastNotifiedAt: entity.LastNotifiedAt,
                    LastDescription: entity.LastDescription);
            }

            _logger.LogInformation("[HealthMonitor] Hydrated {Count} service states from database", entities.Count);
        }
#pragma warning disable CA1031 // Service boundary - hydration failure should not crash service
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[HealthMonitor] Failed to hydrate state from database, starting fresh");
        }
#pragma warning restore CA1031
    }

    private async Task PollHealthChecksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var report = await healthCheckService.CheckHealthAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in report.Entries)
        {
            var serviceName = entry.Key;
            var checkResult = entry.Value.Status;
            var description = entry.Value.Description ?? entry.Value.Exception?.Message;

            // Get or create current state
            if (!_states.TryGetValue(serviceName, out var currentState))
            {
                currentState = new ServiceHealthState(
                    ServiceName: serviceName,
                    CurrentStatus: "Healthy",
                    PreviousStatus: "Healthy",
                    ConsecutiveFailures: 0,
                    ConsecutiveSuccesses: 0,
                    LastTransitionAt: DateTime.UtcNow,
                    LastNotifiedAt: null,
                    LastDescription: null);
            }

            // Evaluate state transition
            var newState = HealthStateMachine.Evaluate(currentState, checkResult, _options);
            newState = newState with { LastDescription = description ?? newState.LastDescription };

            // Resolve tags from the health check registration
            var healthCheckRegistrations = scope.ServiceProvider
                .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
            var registration = healthCheckRegistrations.Registrations
                .FirstOrDefault(r => string.Equals(r.Name, serviceName, StringComparison.OrdinalIgnoreCase));
            var tags = registration?.Tags.ToArray() ?? [];

            var now = DateTime.UtcNow;

            if (HealthStateMachine.HasTransitioned(currentState, newState))
            {
                _logger.LogInformation(
                    "[HealthMonitor] {Service}: {Previous} -> {Current}",
                    serviceName, currentState.CurrentStatus, newState.CurrentStatus);

                newState = newState with { LastNotifiedAt = now };

                await _publisher.Publish(new HealthStatusChangedEvent(
                    ServiceName: serviceName,
                    PreviousStatus: currentState.CurrentStatus,
                    CurrentStatus: newState.CurrentStatus,
                    Description: newState.LastDescription,
                    Tags: tags,
                    TransitionAt: now,
                    IsReminder: false), cancellationToken).ConfigureAwait(false);
            }
            else if (HealthStateMachine.NeedsReminder(newState, _options))
            {
                _logger.LogInformation(
                    "[HealthMonitor] {Service}: Reminder — still {Status}",
                    serviceName, newState.CurrentStatus);

                newState = newState with { LastNotifiedAt = now };

                await _publisher.Publish(new HealthStatusChangedEvent(
                    ServiceName: serviceName,
                    PreviousStatus: newState.PreviousStatus,
                    CurrentStatus: newState.CurrentStatus,
                    Description: newState.LastDescription,
                    Tags: tags,
                    TransitionAt: newState.LastTransitionAt,
                    IsReminder: true), cancellationToken).ConfigureAwait(false);
            }

            // Update in-memory cache
            _states[serviceName] = newState;

            // Persist to database (upsert by ServiceName)
            await UpsertStateAsync(db, newState, tags, cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertStateAsync(
        MeepleAiDbContext db,
        ServiceHealthState state,
        string[] tags,
        CancellationToken cancellationToken)
    {
        var entity = await db.ServiceHealthStates
            .FirstOrDefaultAsync(
                e => e.ServiceName == state.ServiceName,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new ServiceHealthStateEntity
            {
                ServiceName = state.ServiceName,
                CreatedAt = DateTime.UtcNow
            };
            db.ServiceHealthStates.Add(entity);
        }

        entity.CurrentStatus = state.CurrentStatus;
        entity.PreviousStatus = state.PreviousStatus;
        entity.ConsecutiveFailures = state.ConsecutiveFailures;
        entity.ConsecutiveSuccesses = state.ConsecutiveSuccesses;
        entity.LastTransitionAt = state.LastTransitionAt;
        entity.LastNotifiedAt = state.LastNotifiedAt;
        entity.LastDescription = state.LastDescription;
        entity.Tags = System.Text.Json.JsonSerializer.Serialize(tags);
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
