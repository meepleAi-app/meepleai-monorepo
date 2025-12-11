using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Background service for periodic LLM provider health checks
/// ISSUE-962 (BGAI-020): Monitors provider availability and updates health status
/// </summary>
public sealed class ProviderHealthCheckService : BackgroundService, IProviderHealthCheckService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderHealthCheckService> _logger;
    private readonly Dictionary<string, ProviderHealthStatus> _healthStatuses = new(StringComparer.Ordinal);
#pragma warning disable MA0158 // Use System.Threading.Lock
    private readonly object _healthLock = new();
#pragma warning restore MA0158

    private const int CheckIntervalSeconds = 60; // Health check every 60 seconds
    private const int HealthCheckTimeoutSeconds = 5; // 5s timeout for health checks
    private const string HealthCheckPrompt = "ping"; // Simple test prompt

    public ProviderHealthCheckService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProviderHealthCheckService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProviderHealthCheckService starting...");

        // Initialize health statuses
        await InitializeHealthStatuses(stoppingToken).ConfigureAwait(false);

        // Wait 10 seconds before first health check (let app warm up)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthChecksAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check cycle");
            }

            // Wait for next check interval
            await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ProviderHealthCheckService stopping...");
    }

    /// <summary>
    /// Initialize health status tracking for all providers
    /// </summary>
    private Task InitializeHealthStatuses(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IEnumerable<ILlmClient>>();

        lock (_healthLock)
        {
            foreach (var client in clients)
            {
                _healthStatuses[client.ProviderName] = new ProviderHealthStatus();
                _logger.LogInformation(
                    "Health tracking initialized for provider: {Provider}",
                    client.ProviderName);
            }
        }

        _logger.LogInformation(
            "ProviderHealthCheckService initialized with {Count} providers",
            _healthStatuses.Count);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Perform health checks on all providers
    /// </summary>
    private async Task PerformHealthChecksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IEnumerable<ILlmClient>>();

        var tasks = clients.Select(client => CheckProviderHealthAsync(client, ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Log summary
        var summary = GetHealthSummary();
        _logger.LogInformation("Health check cycle complete: {Summary}", summary);
    }

    /// <summary>
    /// Check health of a single provider
    /// </summary>
    private async Task CheckProviderHealthAsync(ILlmClient client, CancellationToken ct)
    {
        var providerName = client.ProviderName;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

            // Determine appropriate model for health check
            var model = GetHealthCheckModel(client);

            // Simple health check request
            var result = await client.GenerateCompletionAsync(
                model,
                "You are a health check system.",
                HealthCheckPrompt,
                temperature: 0.1,
                maxTokens: 10,
                cts.Token).ConfigureAwait(false);

            var success = result.Success && !string.IsNullOrWhiteSpace(result.Response);

            // Update health status
            lock (_healthLock)
            {
                if (_healthStatuses.TryGetValue(providerName, out var status))
                {
                    status.RecordHealthCheck(success);

                    if (success)
                    {
                        _logger.LogDebug(
                            "Health check PASS: {Provider} - {Status}",
                            providerName, status.GetStatusSummary());
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Health check FAIL: {Provider} - {Status}, Error: {Error}",
                            providerName, status.GetStatusSummary(), result.ErrorMessage ?? "unknown");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Health check TIMEOUT: {Provider} (>{Timeout}s)",
                providerName, HealthCheckTimeoutSeconds);

            lock (_healthLock)
            {
                if (_healthStatuses.TryGetValue(providerName, out var status))
                {
                    status.RecordHealthCheck(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Health check ERROR: {Provider}",
                providerName);

            lock (_healthLock)
            {
                if (_healthStatuses.TryGetValue(providerName, out var status))
                {
                    status.RecordHealthCheck(false);
                }
            }
        }
    }

    /// <summary>
    /// Get appropriate model for health check based on provider
    /// </summary>
    private static string GetHealthCheckModel(ILlmClient client)
    {
        // Use fastest/cheapest model for health checks
        return client.ProviderName.ToLowerInvariant() switch
        {
            "ollama" => "llama3:8b",
            "openrouter" => "meta-llama/llama-3.3-70b-instruct:free", // Free tier
            _ => "llama3:8b" // Default
        };
    }

    /// <summary>
    /// Get health status for a specific provider
    /// </summary>
    public ProviderHealthStatus? GetProviderHealth(string providerName)
    {
        lock (_healthLock)
        {
            return _healthStatuses.TryGetValue(providerName, out var status) ? status : null;
        }
    }

    /// <summary>
    /// Get health status for all providers
    /// </summary>
    public Dictionary<string, ProviderHealthStatus> GetAllProviderHealth()
    {
        lock (_healthLock)
        {
            return new Dictionary<string, ProviderHealthStatus>(_healthStatuses, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Get health summary for logging
    /// </summary>
    private string GetHealthSummary()
    {
        lock (_healthLock)
        {
            var healthy = _healthStatuses.Count(h => h.Value.Status == HealthStatus.Healthy);
            var degraded = _healthStatuses.Count(h => h.Value.Status == HealthStatus.Degraded);
            var unhealthy = _healthStatuses.Count(h => h.Value.Status == HealthStatus.Unhealthy);
            var unknown = _healthStatuses.Count(h => h.Value.Status == HealthStatus.Unknown);

            return $"Healthy: {healthy}, Degraded: {degraded}, Unhealthy: {unhealthy}, Unknown: {unknown}";
        }
    }
}
