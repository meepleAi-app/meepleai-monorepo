namespace Api.Services.Providers.Probe;

/// <summary>
/// Resolves an <see cref="IProviderProbeExecutor"/> by provider name. Issue #972.
/// </summary>
internal interface IProviderProbeExecutorFactory
{
    IProviderProbeExecutor? GetExecutor(string providerName);

    IReadOnlyCollection<string> KnownProviderNames { get; }
}
