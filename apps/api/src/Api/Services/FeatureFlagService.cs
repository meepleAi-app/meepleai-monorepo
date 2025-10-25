using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// CONFIG-05: Feature flags service implementation.
/// Provides runtime feature toggling with role-based access control.
/// Builds on CONFIG-01's ConfigurationService infrastructure.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<FeatureFlagService> _logger;
    private const string FeatureFlagCategory = "FeatureFlags";

    public FeatureFlagService(
        IConfigurationService configService,
        ILogger<FeatureFlagService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Check if a feature is enabled with role-based hierarchy.
    /// Hierarchy: Role-specific flag > Global flag > Default false
    /// </summary>
    public async Task<bool> IsEnabledAsync(string featureName, UserRole? role = null)
    {
        // Try role-specific flag first (highest priority)
        if (role.HasValue)
        {
            var roleFlagKey = $"{featureName}.{role.Value}";
            var roleSpecificFlag = await _configService.GetValueAsync<bool?>(roleFlagKey, null);

            if (roleSpecificFlag.HasValue)
            {
                _logger.LogDebug("Feature {FeatureName} for role {Role}: {Enabled} (role-specific)",
                    featureName, role.Value, roleSpecificFlag.Value);
                return roleSpecificFlag.Value;
            }
        }

        // Fall back to global flag
        var globalFlag = await _configService.GetValueAsync<bool?>(featureName, null);

        if (globalFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName}: {Enabled} (global)", featureName, globalFlag.Value);
            return globalFlag.Value;
        }

        // Default: feature disabled (fail-safe)
        _logger.LogDebug("Feature {FeatureName}: false (default - not found)", featureName);
        return false;
    }

    /// <summary>
    /// Enable a feature globally or for a specific role.
    /// </summary>
    public async Task EnableFeatureAsync(string featureName, UserRole? role = null, string? userId = null)
    {
        var key = role.HasValue ? $"{featureName}.{role.Value}" : featureName;

        // Check if configuration exists
        var existing = await _configService.GetConfigurationByKeyAsync(key);

        if (existing != null)
        {
            // Update existing configuration
            var updateRequest = new UpdateConfigurationRequest(
                Value: "true",
                Description: existing.Description,
                IsActive: true,
                RequiresRestart: false);

            await _configService.UpdateConfigurationAsync(existing.Id, updateRequest, userId ?? "system");

            _logger.LogInformation("Feature {FeatureName} enabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
        else
        {
            // Create new configuration
            var createRequest = new CreateConfigurationRequest(
                Key: key,
                Value: "true",
                ValueType: "Boolean",
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                IsActive: true,
                RequiresRestart: false,
                Environment: "Production");

            await _configService.CreateConfigurationAsync(createRequest, userId ?? "system");

            _logger.LogInformation("Feature {FeatureName} created and enabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
    }

    /// <summary>
    /// Disable a feature globally or for a specific role.
    /// </summary>
    public async Task DisableFeatureAsync(string featureName, UserRole? role = null, string? userId = null)
    {
        var key = role.HasValue ? $"{featureName}.{role.Value}" : featureName;

        // Check if configuration exists
        var existing = await _configService.GetConfigurationByKeyAsync(key);

        if (existing != null)
        {
            // Update existing configuration
            var updateRequest = new UpdateConfigurationRequest(
                Value: "false",
                Description: existing.Description,
                IsActive: true,
                RequiresRestart: false);

            await _configService.UpdateConfigurationAsync(existing.Id, updateRequest, userId ?? "system");

            _logger.LogInformation("Feature {FeatureName} disabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
        else
        {
            // Create new configuration as disabled
            var createRequest = new CreateConfigurationRequest(
                Key: key,
                Value: "false",
                ValueType: "Boolean",
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                IsActive: true,
                RequiresRestart: false,
                Environment: "Production");

            await _configService.CreateConfigurationAsync(createRequest, userId ?? "system");

            _logger.LogInformation("Feature {FeatureName} created and disabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
    }

    /// <summary>
    /// List all feature flags with their current states.
    /// </summary>
    public async Task<List<FeatureFlagDto>> GetAllFeatureFlagsAsync()
    {
        // Get all feature flag configurations (category = "FeatureFlags")
        var configs = await _configService.GetConfigurationsAsync(
            category: FeatureFlagCategory,
            activeOnly: true,
            pageSize: 100);

        var flags = new List<FeatureFlagDto>();

        foreach (var config in configs.Items)
        {
            // Parse role restriction from key (e.g., "Features.RagEvaluation.Admin")
            var parts = config.Key.Split('.');
            var roleRestriction = parts.Length > 2 ? parts[^1] : null;
            var featureName = roleRestriction != null
                ? string.Join(".", parts[..^1])
                : config.Key;

            // Parse boolean value
            var isEnabled = bool.TryParse(config.Value, out var enabled) && enabled;

            flags.Add(new FeatureFlagDto(
                FeatureName: featureName,
                IsEnabled: isEnabled,
                RoleRestriction: roleRestriction,
                Description: config.Description ?? $"Feature flag: {featureName}"));
        }

        return flags;
    }
}
