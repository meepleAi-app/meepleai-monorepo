namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Maps a provider name to the conventional environment-variable name from which the API key is
/// read when no DB-backed credential row exists. Used by
/// <see cref="ProviderCredentialResolver"/> as the fallback step of its DB → env-var cascade.
/// Issue #1859.
/// </summary>
internal static class ProviderEnvVarMap
{
    /// <summary>
    /// Returns the env-var name conventionally used for the provider's API key.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="providerName"/> is empty or not in the supported set.
    /// </exception>
    public static string For(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return providerName.ToLowerInvariant() switch
        {
            "deepseek" => "DEEPSEEK_API_KEY",
            "openrouter" => "OPENROUTER_API_KEY",
            _ => throw new ArgumentException(
                $"Unknown provider '{providerName}' — no env-var mapping configured",
                nameof(providerName))
        };
    }
}
