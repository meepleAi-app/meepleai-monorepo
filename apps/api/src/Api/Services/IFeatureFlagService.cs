using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure.Entities;

namespace Api.Services;

/// <summary>
/// Service for managing feature flags - runtime feature toggling without deployment.
/// Supports role-based and tier-based access control and integrates with CONFIG-01 infrastructure.
/// Issue #3073: Extended to support tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal interface IFeatureFlagService
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
    /// Check if a feature is enabled for a specific tier.
    /// Supports tier-based feature access (e.g., feature enabled only for premium users).
    /// Hierarchy: Tier-specific flags override global flags.
    /// </summary>
    /// <param name="featureName">Feature flag name (e.g., "Features.RAG")</param>
    /// <param name="tier">User tier for tier-specific feature access</param>
    /// <returns>True if feature is enabled for the tier, false otherwise</returns>
    Task<bool> IsEnabledForTierAsync(string featureName, UserTier tier);

    /// <summary>
    /// Check if a user can access a feature based on both role AND tier.
    /// Combined logic: roleAccess AND tierAccess must both be true.
    /// </summary>
    /// <param name="user">The user to check access for</param>
    /// <param name="featureName">Feature flag name</param>
    /// <returns>True if user can access the feature, false otherwise</returns>
    Task<bool> CanAccessFeatureAsync(User user, string featureName);

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
    /// Enable a feature for a specific tier.
    /// </summary>
    /// <param name="featureName">Feature flag name</param>
    /// <param name="tier">User tier for tier-specific enabling</param>
    /// <param name="userId">User ID performing the operation (for audit trail)</param>
    Task EnableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null);

    /// <summary>
    /// Disable a feature for a specific tier.
    /// </summary>
    /// <param name="featureName">Feature flag name</param>
    /// <param name="tier">User tier for tier-specific disabling</param>
    /// <param name="userId">User ID performing the operation (for audit trail)</param>
    Task DisableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null);

    /// <summary>
    /// List all feature flags with their current states.
    /// </summary>
    /// <returns>List of all feature flag DTOs</returns>
    Task<List<Models.FeatureFlagDto>> GetAllFeatureFlagsAsync();
}
