using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for managing system-wide dynamic configuration.
/// Enables runtime configuration changes without redeployment.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Get a configuration by its key (internal infrastructure method).
    /// </summary>
    /// <param name="key">Configuration key (e.g., "RateLimit:Admin:MaxTokens")</param>
    /// <param name="environment">Optional environment filter (defaults to current environment)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a typed configuration value by key.
    /// </summary>
    /// <typeparam name="T">Expected value type</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <param name="environment">Optional environment filter</param>
    /// <returns>Typed configuration value or default</returns>
    Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null);
}
