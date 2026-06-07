using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// Thrown by <c>IProviderCredentialResolver</c> when neither a DB-backed credential row nor an
/// env-var fallback is configured for the requested provider. Indicates a configuration error
/// (deployment misstep or DB row accidentally deactivated). Maps to HTTP 503 Service Unavailable
/// with subcode <c>provider_credential_not_configured</c>.
/// Issue #1859.
/// </summary>
public sealed class ProviderCredentialNotConfiguredException : HttpException
{
    public string ProviderName { get; }

    [SetsRequiredMembers]
    public ProviderCredentialNotConfiguredException(string providerName)
        : base(503,
            "provider_credential_not_configured",
            $"No active credential or env-var configured for provider '{providerName}'")
    {
        ProviderName = providerName;
    }
}
