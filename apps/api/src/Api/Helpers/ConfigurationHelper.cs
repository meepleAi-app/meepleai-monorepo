using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Helpers;

/// <summary>
/// Helper for standardizing the 3-tier configuration fallback pattern:
/// Database → appsettings.json → hardcoded defaults
/// </summary>
public class ConfigurationHelper
{
    private readonly IConfigurationService _configService;
    private readonly IConfiguration _fallbackConfig;
    private readonly ILogger<ConfigurationHelper> _logger;

    public ConfigurationHelper(
        IConfigurationService configService,
        IConfiguration fallbackConfig,
        ILogger<ConfigurationHelper> logger)
    {
        _configService = configService;
        _fallbackConfig = fallbackConfig;
        _logger = logger;
    }

    /// <summary>
    /// Gets a configuration value using the 3-tier fallback pattern:
    /// 1. Try database (via IConfigurationService)
    /// 2. Fall back to appsettings.json (via IConfiguration)
    /// 3. Fall back to provided default value
    /// </summary>
    public async Task<T> GetValueAsync<T>(
        string key,
        T defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        // Tier 1: Try database
        try
        {
            // Check if configuration exists in database (to distinguish "not found" from "found with default value")
            var dbConfig = await _configService.GetConfigurationByKeyAsync(key, environment);
            if (dbConfig != null && dbConfig.IsActive)
            {
                // Configuration exists, get the typed value even if it equals default(T)
                var dbValue = await _configService.GetValueAsync(key, default(T), environment);
                _logger.LogDebug("Configuration {Key} loaded from database: {Value}", key, dbValue);
                return dbValue!;
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration {Key} from database, falling back to appsettings", key);
        }
#pragma warning restore CA1031

        // Tier 2: Try appsettings.json
        try
        {
            // Check if key exists in appsettings (to distinguish "not found" from "found with default value")
            var section = _fallbackConfig.GetSection(key);
            if (section.Exists())
            {
                // Key exists, get the value even if it equals default(T)
                var configValue = _fallbackConfig.GetValue<T>(key);
                _logger.LogDebug("Configuration {Key} loaded from appsettings.json: {Value}", key, configValue);
                return configValue!;
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration {Key} from appsettings.json, using default", key);
        }
#pragma warning restore CA1031

        // Tier 3: Use default value
        _logger.LogDebug("Configuration {Key} using default value: {Value}", key, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// Gets a string configuration value with 3-tier fallback.
    /// </summary>
    public async Task<string> GetStringAsync(
        string key,
        string defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        return await GetValueAsync(key, defaultValue, environment, ct);
    }

    /// <summary>
    /// Gets an int configuration value with 3-tier fallback.
    /// </summary>
    public async Task<int> GetIntAsync(
        string key,
        int defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        return await GetValueAsync(key, defaultValue, environment, ct);
    }

    /// <summary>
    /// Gets a bool configuration value with 3-tier fallback.
    /// </summary>
    public async Task<bool> GetBoolAsync(
        string key,
        bool defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        return await GetValueAsync(key, defaultValue, environment, ct);
    }

    /// <summary>
    /// Gets a double configuration value with 3-tier fallback.
    /// </summary>
    public async Task<double> GetDoubleAsync(
        string key,
        double defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        return await GetValueAsync(key, defaultValue, environment, ct);
    }

    /// <summary>
    /// Gets a long configuration value with 3-tier fallback.
    /// </summary>
    public async Task<long> GetLongAsync(
        string key,
        long defaultValue,
        string? environment = null,
        CancellationToken ct = default)
    {
        return await GetValueAsync(key, defaultValue, environment, ct);
    }
}
