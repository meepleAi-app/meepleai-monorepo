using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Rag;

/// <summary>
/// Implementation of RAG configuration provider with 3-tier fallback strategy.
/// Priority: 1) Database (via ConfigurationService) → 2) appsettings.json → 3) Hardcoded defaults
/// Issue #1441: Extracted from RagService to reduce complexity and improve testability.
/// </summary>
public class RagConfigurationProvider : IRagConfigurationProvider
{
    private readonly IConfigurationService? _configurationService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<RagConfigurationProvider> _logger;

    public RagConfigurationProvider(
        ILogger<RagConfigurationProvider> logger,
        IConfigurationService? configurationService = null,
        IConfiguration? configuration = null)
    {
        _logger = logger;
        _configurationService = configurationService;
        _configuration = configuration;
    }

    /// <summary>
    /// Get RAG configuration with 3-tier fallback (DB → appsettings.json → hardcoded)
    /// </summary>
    public async Task<T> GetRagConfigAsync<T>(string configKey, T defaultValue) where T : struct
    {
        // 1. Try database via ConfigurationService
        if (_configurationService != null)
        {
            var dbValue = await _configurationService.GetValueAsync<T?>($"RAG.{configKey}");
            if (dbValue.HasValue)
            {
                var validated = ValidateRagConfig(dbValue.Value, configKey);
                _logger.LogDebug("RAG config {Key}: {Value} (from database)", configKey, validated);
                return validated;
            }
        }

        // 2. Try appsettings.json
        if (_configuration != null)
        {
            var configPath = $"RAG:{configKey}";
            var appSettingsValue = _configuration.GetValue<T?>(configPath);
            if (appSettingsValue.HasValue)
            {
                var validated = ValidateRagConfig(appSettingsValue.Value, configKey);
                _logger.LogDebug("RAG config {Key}: {Value} (from appsettings)", configKey, validated);
                return validated;
            }
        }

        // 3. Fall back to hardcoded default
        _logger.LogDebug("RAG config {Key}: {Value} (using hardcoded default)", configKey, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// Validate RAG configuration value is within acceptable range
    /// </summary>
    private T ValidateRagConfig<T>(T value, string configKey) where T : struct
    {
        var numericValue = Convert.ToDouble(value);
        bool isValid = configKey switch
        {
            "TopK" => numericValue >= 1 && numericValue <= 50,
            "MinScore" => numericValue >= 0.0 && numericValue <= 1.0,
            "RrfK" => numericValue >= 1 && numericValue <= 100,
            "MaxQueryVariations" => numericValue >= 1 && numericValue <= 10,
            _ => true
        };

        if (!isValid)
        {
            _logger.LogWarning(
                "RAG config {Key}={Value} out of range, clamping",
                configKey, value);

            return configKey switch
            {
                "TopK" => (T)(object)Math.Clamp((int)numericValue, 1, 50),
                "MinScore" => (T)(object)Math.Clamp(numericValue, 0.0, 1.0),
                "RrfK" => (T)(object)Math.Clamp((int)numericValue, 1, 100),
                "MaxQueryVariations" => (T)(object)Math.Clamp((int)numericValue, 1, 10),
                _ => value
            };
        }

        return value;
    }
}
