namespace Api.Services.Rag;

/// <summary>
/// Provides RAG-specific configuration with 3-tier fallback strategy.
/// Issue #1441: Extracted from RagService to reduce complexity and improve separation of concerns.
/// </summary>
public interface IRagConfigurationProvider
{
    /// <summary>
    /// Gets RAG configuration value with 3-tier fallback (DB → appsettings.json → hardcoded defaults).
    /// </summary>
    /// <typeparam name="T">Value type (must be a struct)</typeparam>
    /// <param name="configKey">Configuration key (e.g., "TopK", "MinScore")</param>
    /// <param name="defaultValue">Hardcoded fallback value</param>
    /// <returns>Configuration value from highest priority available source</returns>
    Task<T> GetRagConfigAsync<T>(string configKey, T defaultValue) where T : struct;
}
