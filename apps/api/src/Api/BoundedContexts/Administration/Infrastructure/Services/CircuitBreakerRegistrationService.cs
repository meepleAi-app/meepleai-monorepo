using Microsoft.Extensions.Hosting;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Registers all known external service names with the circuit breaker tracker at startup,
/// so the admin panel can display their state even before the first call is made.
/// </summary>
internal sealed class CircuitBreakerRegistrationService : IHostedService
{
    private readonly ICircuitBreakerStateTracker _tracker;

    private static readonly string[] ServiceNames =
    [
        "OpenRouter", "Ollama", "EmbeddingService", "HuggingFace",
        "BggApi", "Infisical", "OrchestrationService",
        "UnstructuredService", "SmolDoclingService"
    ];

    public CircuitBreakerRegistrationService(ICircuitBreakerStateTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var name in ServiceNames)
            _tracker.RegisterService(name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
