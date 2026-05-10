namespace Api.Services.Providers.Probe;

/// <summary>
/// Resolves an <see cref="IProviderProbeExecutor"/> by provider name (case-insensitive).
/// </summary>
internal sealed class ProviderProbeExecutorFactory
{
    private readonly Dictionary<string, IProviderProbeExecutor> _executors;
    private readonly IReadOnlyCollection<string> _knownProviderNames;

    public ProviderProbeExecutorFactory(IEnumerable<IProviderProbeExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.ProviderName, StringComparer.OrdinalIgnoreCase);
        _knownProviderNames = _executors.Keys.ToList().AsReadOnly();
    }

    public IProviderProbeExecutor? GetExecutor(string providerName)
        => _executors.TryGetValue(providerName, out var ex) ? ex : null;

    public IReadOnlyCollection<string> KnownProviderNames => _knownProviderNames;
}
