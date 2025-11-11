using Api.Infrastructure.Entities;

namespace Api.Services;

/// <summary>
/// Service for managing feature flags - runtime feature toggling without deployment.
/// Supports role-based access control and integrates with CONFIG-01 infrastructure.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Check if a feature is enabled for the current context.
    /// Supports role-based feature access (e.g., feature enabled only for admins).
    /// Hierarchy: Role-specific flags override global flags.
    /// </summary>
    /// <param name="featureName">Feature flag name (e.g., "Features.ChatExport")</param>
    /// <param name="role">Optional user role for role-specific feature access</param>
    /// <returns>True if feature is enabled, false otherwise</returns>
    Task<bool> IsEnabledAsync(string featureName, UserRole? role = null);

    /// <summary>
    /// Enable a feature globally or for a specific role.
    /// </summary>
    /// <param name="featureName">Feature flag name</param>
    /// <param name="role">Optional role for role-specific enabling</param>
    /// <param name="userId">User ID performing the operation (for audit trail)</param>
    Task EnableFeatureAsync(string featureName, UserRole? role = null, string? userId = null);

    /// <summary>
    /// Disable a feature globally or for a specific role.
    /// </summary>
    /// <param name="featureName">Feature flag name</param>
    /// <param name="role">Optional role for role-specific disabling</param>
    /// <param name="userId">User ID performing the operation (for audit trail)</param>
    Task DisableFeatureAsync(string featureName, UserRole? role = null, string? userId = null);

    /// <summary>
    /// List all feature flags with their current states.
    /// </summary>
    /// <returns>List of all feature flag DTOs</returns>
    Task<List<Models.FeatureFlagDto>> GetAllFeatureFlagsAsync();
}
