namespace Api.Services.Providers.Quota;

/// <summary>
/// Resolves an <see cref="IProviderQuotaProvider"/> by provider name. Issue #972.
/// </summary>
internal interface IProviderQuotaProviderFactory
{
    IProviderQuotaProvider? GetProvider(string providerName);

    IReadOnlyCollection<string> SupportedProviderNames { get; }
}
