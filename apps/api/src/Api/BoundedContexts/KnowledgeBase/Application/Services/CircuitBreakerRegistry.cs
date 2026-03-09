using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5487: Shared circuit breaker registry extracted from HybridLlmService.
/// Manages circuit breaker state and latency tracking for all LLM providers.
/// Singleton — state must be shared across all scoped HybridLlmService instances.
/// </summary>
internal sealed class CircuitBreakerRegistry : ICircuitBreakerRegistry
{
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LatencyStats> _latencyStats = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _monitoringLock = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CircuitBreakerRegistry> _logger;

    public CircuitBreakerRegistry(
        IServiceScopeFactory scopeFactory,
        ILogger<CircuitBreakerRegistry> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void Initialize(string providerName)
    {
        lock (_monitoringLock)
        {
            if (_circuitBreakers.ContainsKey(providerName))
                return;

            var breaker = new CircuitBreakerState();
            var capturedProvider = providerName;
            breaker.OnStateTransition = (prev, next) => PublishCircuitBreakerEvent(capturedProvider, prev, next);
            _circuitBreakers[providerName] = breaker;
            _latencyStats[providerName] = new LatencyStats();

            _logger.LogDebug("Circuit breaker initialized for provider {Provider}", providerName);
        }
    }

    /// <inheritdoc/>
    public bool AllowsRequests(string providerName)
    {
        lock (_monitoringLock)
        {
            return !_circuitBreakers.TryGetValue(providerName, out var breaker) || breaker.AllowsRequests();
        }
    }

    /// <inheritdoc/>
    public CircuitState GetState(string providerName)
    {
        lock (_monitoringLock)
        {
            return _circuitBreakers.TryGetValue(providerName, out var breaker)
                ? breaker.State
                : CircuitState.Closed;
        }
    }

    /// <inheritdoc/>
    public void RecordSuccess(string providerName, long latencyMs)
    {
        lock (_monitoringLock)
        {
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker.RecordSuccess();
            }

            if (_latencyStats.TryGetValue(providerName, out var stats))
            {
                stats.RecordLatency(latencyMs);
            }

            _logger.LogDebug(
                "Request success: {Provider} - Latency: {Latency}ms, Circuit: {CircuitState}",
                providerName, latencyMs, breaker?.GetStatus() ?? "unknown");
        }
    }

    /// <inheritdoc/>
    public void RecordFailure(string providerName, long latencyMs)
    {
        lock (_monitoringLock)
        {
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker.RecordFailure();
            }

            if (_latencyStats.TryGetValue(providerName, out var stats))
            {
                stats.RecordLatency(latencyMs);
            }

            _logger.LogWarning(
                "Request failure: {Provider} - Latency: {Latency}ms, Circuit: {CircuitState}",
                providerName, latencyMs, breaker?.GetStatus() ?? "unknown");
        }
    }

    /// <inheritdoc/>
    public void ResetCircuitBreaker(string? targetProvider = null)
    {
        lock (_monitoringLock)
        {
            if (targetProvider != null)
            {
                if (_circuitBreakers.TryGetValue(targetProvider, out var breaker))
                {
                    breaker.Reset();
                    _logger.LogWarning("Circuit breaker reset for provider {Provider}", targetProvider);
                }
            }
            else
            {
                foreach (var (provider, breaker) in _circuitBreakers)
                {
                    breaker.Reset();
                    _logger.LogWarning("Circuit breaker reset for provider {Provider} (reset-all)", provider);
                }
            }
        }
    }

    /// <inheritdoc/>
    public string GetCircuitStateDescription(string providerName)
    {
        lock (_monitoringLock)
        {
            return _circuitBreakers.TryGetValue(providerName, out var breaker)
                ? breaker.GetStatus()
                : "unknown";
        }
    }

    /// <inheritdoc/>
    public string GetLatencyStats(string providerName)
    {
        lock (_monitoringLock)
        {
            return _latencyStats.TryGetValue(providerName, out var stats)
                ? stats.GetSummary()
                : "No data";
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, (string circuitState, string latencyStats)> GetMonitoringStatus()
    {
        lock (_monitoringLock)
        {
            var status = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (var (providerName, breaker) in _circuitBreakers)
            {
                var circuit = breaker.GetStatus();

                var latency = _latencyStats.TryGetValue(providerName, out var stats)
                    ? stats.GetSummary()
                    : "No data";

                status[providerName] = (circuit, latency);
            }
            return status;
        }
    }

    /// <summary>
    /// Issue #5086: Fire-and-forget circuit breaker state change notification.
    /// </summary>
    private void PublishCircuitBreakerEvent(string providerName, CircuitState previousState, CircuitState newState)
    {
        var reason = (previousState, newState) switch
        {
            (CircuitState.Closed, CircuitState.Open) =>
                $"{providerName} circuit breaker OPENED: consecutive failure threshold reached. Falling back to alternative provider.",
            (CircuitState.HalfOpen, CircuitState.Open) =>
                $"{providerName} circuit breaker REOPENED: recovery attempt failed.",
            (CircuitState.HalfOpen, CircuitState.Closed) =>
                $"{providerName} circuit breaker CLOSED: service recovered successfully.",
            (CircuitState.Open, CircuitState.HalfOpen) =>
                $"{providerName} circuit breaker HALF-OPEN: testing recovery.",
            _ => $"{providerName} circuit breaker transitioned {previousState} → {newState}."
        };

        var evt = new CircuitBreakerStateChangedEvent(providerName, previousState, newState, reason, DateTime.UtcNow);
        var scopeFactory = _scopeFactory;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(evt, CancellationToken.None).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish CircuitBreakerStateChangedEvent for {Provider}", providerName);
            }
#pragma warning restore CA1031
        });
    }
}
