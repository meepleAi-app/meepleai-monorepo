using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// CONFIG-05: Feature flags service implementation.
/// Provides runtime feature toggling with role-based and tier-based access control.
/// Uses CQRS commands/queries for configuration management.
/// Issue #3073: Extended to support tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfigurationService _configService;
    private readonly IMediator _mediator;
    private readonly ILogger<FeatureFlagService> _logger;
    private const string FeatureFlagCategory = "FeatureFlags";
    private const string TierPrefix = "Tier";

    public FeatureFlagService(
        IConfigurationService configService,
        IMediator mediator,
        ILogger<FeatureFlagService> logger)
    {
        _configService = configService;
        _mediator = mediator;
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
            var roleSpecificFlag = await _configService.GetValueAsync<bool?>(roleFlagKey, null).ConfigureAwait(false);

            if (roleSpecificFlag.HasValue)
            {
                _logger.LogDebug("Feature {FeatureName} for role {Role}: {Enabled} (role-specific)",
                    featureName, role.Value, roleSpecificFlag.Value);
                return roleSpecificFlag.Value;
            }
        }

        // Fall back to global flag
        var globalFlag = await _configService.GetValueAsync<bool?>(featureName, null).ConfigureAwait(false);

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
    /// Check if a feature is enabled for a specific tier.
    /// Hierarchy: Tier-specific flag > Global flag > Default true (for backward compatibility)
    /// </summary>
    public async Task<bool> IsEnabledForTierAsync(string featureName, UserTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        // Try tier-specific flag first (highest priority)
        var tierFlagKey = $"{featureName}.{TierPrefix}.{tier.Value}";
        var tierSpecificFlag = await _configService.GetValueAsync<bool?>(tierFlagKey, null).ConfigureAwait(false);

        if (tierSpecificFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName} for tier {Tier}: {Enabled} (tier-specific)",
                featureName, tier.Value, tierSpecificFlag.Value);
            return tierSpecificFlag.Value;
        }

        // Fall back to global flag
        var globalFlag = await _configService.GetValueAsync<bool?>(featureName, null).ConfigureAwait(false);

        if (globalFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName}: {Enabled} (global)", featureName, globalFlag.Value);
            return globalFlag.Value;
        }

        // Default: feature enabled for tier (backward compatibility - all tiers allowed by default)
        _logger.LogDebug("Feature {FeatureName} for tier {Tier}: true (default - not found)", featureName, tier.Value);
        return true;
    }

    /// <summary>
    /// Check if a user can access a feature based on both role AND tier.
    /// Combined logic: roleAccess AND tierAccess must both be true.
    /// </summary>
    public async Task<bool> CanAccessFeatureAsync(User user, string featureName)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name cannot be empty", nameof(featureName));

        // Map User.Role (ValueObject) to UserRole enum
        var userRole = MapRoleToUserRole(user.Role);

        // Check role-based access
        var roleAccess = await IsEnabledAsync(featureName, userRole).ConfigureAwait(false);
        if (!roleAccess)
        {
            _logger.LogDebug("Feature {FeatureName} denied for user {UserId}: role-based access denied",
                featureName, user.Id);
            return false;
        }

        // Check tier-based access
        var tierAccess = await IsEnabledForTierAsync(featureName, user.Tier).ConfigureAwait(false);
        if (!tierAccess)
        {
            _logger.LogDebug("Feature {FeatureName} denied for user {UserId}: tier-based access denied",
                featureName, user.Id);
            return false;
        }

        _logger.LogDebug("Feature {FeatureName} granted for user {UserId} (role={Role}, tier={Tier})",
            featureName, user.Id, user.Role, user.Tier);
        return true;
    }

    /// <summary>
    /// Maps Role value object to UserRole enum for feature flag checking.
    /// </summary>
    private static UserRole MapRoleToUserRole(Role role)
    {
        if (role.IsAdmin()) return UserRole.Admin;
        if (role.IsEditor()) return UserRole.Editor;
        return UserRole.User;
    }

    /// <summary>
    /// Enable a feature globally or for a specific role.
    /// </summary>
    public async Task EnableFeatureAsync(string featureName, UserRole? role = null, string? userId = null)
    {
        var key = role.HasValue ? $"{featureName}.{role.Value}" : featureName;

        // Check if configuration exists
        var existing = await _configService.GetConfigurationByKeyAsync(key).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "true",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} enabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
        else
        {
            // Create new configuration
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "true",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                Environment: "Production",
                RequiresRestart: false);

            await _mediator.Send(command).ConfigureAwait(false);

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
        var existing = await _configService.GetConfigurationByKeyAsync(key).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "false",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} disabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
        else
        {
            // Create new configuration as disabled
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "false",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                Environment: "Production",
                RequiresRestart: false);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and disabled{RoleInfo} by {UserId}",
                featureName, role.HasValue ? $" for {role.Value}" : "", userId ?? "system");
        }
    }

    /// <summary>
    /// Enable a feature for a specific tier.
    /// </summary>
    public async Task EnableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(tier);
        var key = $"{featureName}.{TierPrefix}.{tier.Value}";

        // Check if configuration exists
        var existing = await _configService.GetConfigurationByKeyAsync(key).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "true",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} enabled for tier {Tier} by {UserId}",
                featureName, tier.Value, userId ?? "system");
        }
        else
        {
            // Create new configuration
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "true",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName} (tier: {tier.Value})",
                Category: FeatureFlagCategory,
                Environment: "Production",
                RequiresRestart: false);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and enabled for tier {Tier} by {UserId}",
                featureName, tier.Value, userId ?? "system");
        }
    }

    /// <summary>
    /// Disable a feature for a specific tier.
    /// </summary>
    public async Task DisableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(tier);
        var key = $"{featureName}.{TierPrefix}.{tier.Value}";

        // Check if configuration exists
        var existing = await _configService.GetConfigurationByKeyAsync(key).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "false",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} disabled for tier {Tier} by {UserId}",
                featureName, tier.Value, userId ?? "system");
        }
        else
        {
            // Create new configuration as disabled
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "false",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName} (tier: {tier.Value})",
                Category: FeatureFlagCategory,
                Environment: "Production",
                RequiresRestart: false);

            await _mediator.Send(command).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and disabled for tier {Tier} by {UserId}",
                featureName, tier.Value, userId ?? "system");
        }
    }

    /// <summary>
    /// List all feature flags with their current states.
    /// Issue #3073: Extended to include tier restrictions.
    /// </summary>
    public async Task<List<FeatureFlagDto>> GetAllFeatureFlagsAsync()
    {
        // Get all feature flag configurations (category = "FeatureFlags")
        var query = new GetAllConfigsQuery(
            Category: FeatureFlagCategory,
            Environment: null,
            ActiveOnly: true,
            Page: 1,
            PageSize: 100);
        var configs = await _mediator.Send(query).ConfigureAwait(false);

        var flags = new List<FeatureFlagDto>();

        foreach (var config in configs.Items)
        {
            // Parse restriction from key
            // Formats: "FeatureName", "FeatureName.Role", "FeatureName.Tier.TierName"
            var parts = config.Key.Split('.');
            string? roleRestriction = null;
            string? tierRestriction = null;
            string featureName;

            // Check for tier-based key format: "FeatureName.Tier.TierName"
            if (parts.Length >= 3 && string.Equals(parts[^2], TierPrefix, StringComparison.Ordinal))
            {
                tierRestriction = parts[^1];
                featureName = string.Join(".", parts[..^2]);
            }
            // Check for role-based key format: "FeatureName.Role"
            else if (parts.Length > 1)
            {
                var lastPart = parts[^1];
                // Check if last part is a valid UserRole (Admin, Editor, User)
                if (Enum.TryParse<UserRole>(lastPart, ignoreCase: true, out _))
                {
                    roleRestriction = lastPart;
                    featureName = string.Join(".", parts[..^1]);
                }
                else
                {
                    featureName = config.Key;
                }
            }
            else
            {
                featureName = config.Key;
            }

            // Parse boolean value
            var isEnabled = bool.TryParse(config.Value, out var enabled) && enabled;

            flags.Add(new FeatureFlagDto(
                FeatureName: featureName,
                IsEnabled: isEnabled,
                RoleRestriction: roleRestriction,
                TierRestriction: tierRestriction,
                Description: config.Description ?? $"Feature flag: {featureName}"));
        }

        return flags;
    }
}
