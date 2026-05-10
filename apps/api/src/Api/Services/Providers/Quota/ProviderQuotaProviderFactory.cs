namespace Api.Services.Providers.Quota;

/// <summary>
/// Resolves an <see cref="IProviderQuotaProvider"/> by provider name (case-insensitive).
/// Issue #936 (G2).
/// </summary>
internal sealed class ProviderQuotaProviderFactory
{
    private readonly Dictionary<string, IProviderQuotaProvider> _providers;
    private readonly IReadOnlyCollection<string> _supportedProviderNames;

    public ProviderQuotaProviderFactory(IEnumerable<IProviderQuotaProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _supportedProviderNames = _providers.Keys.ToList().AsReadOnly();
    }

    public IProviderQuotaProvider? GetProvider(string providerName)
        => _providers.TryGetValue(providerName, out var p) ? p : null;

    public IReadOnlyCollection<string> SupportedProviderNames => _supportedProviderNames;
}
