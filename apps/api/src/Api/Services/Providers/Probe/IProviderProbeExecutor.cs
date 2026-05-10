namespace Api.Services.Providers.Probe;

/// <summary>
/// Strategy for executing a list-models probe against a specific provider.
/// Implementations must enforce 5s hard timeout and never throw on network failures.
/// </summary>
internal interface IProviderProbeExecutor
{
    string ProviderName { get; }
    Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken);
}
