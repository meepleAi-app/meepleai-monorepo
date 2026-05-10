namespace Api.Services.Providers.Probe;

/// <summary>
/// Strategy for executing a list-models probe against a specific provider.
/// Implementations must enforce a hard timeout and never throw on network failures.
/// </summary>
internal interface IProviderProbeExecutor
{
    /// <summary>Unique identifier for this provider (e.g., "openrouter", "deepseek", "ollama-local").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Environment variable name from which to read the API key.
    /// Null = provider does not require authentication (e.g., local Ollama).
    /// </summary>
    string? ApiKeyEnvVar { get; }

    Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken);
}
