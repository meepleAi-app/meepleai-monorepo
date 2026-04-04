using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using NotFoundException = Api.Middleware.Exceptions.NotFoundException;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Handles retrieval of features available to a specific user.
/// Combines role and tier checks to determine access.
/// Issue #3674: Feature Flags Verification - User features endpoint
/// </summary>
internal sealed class GetUserAvailableFeaturesQueryHandler : IQueryHandler<GetUserAvailableFeaturesQuery, IReadOnlyList<UserFeatureDto>>
{
    private readonly IUserProfileReadService _userProfileReadService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<GetUserAvailableFeaturesQueryHandler> _logger;

    public GetUserAvailableFeaturesQueryHandler(
        IUserProfileReadService userProfileReadService,
        IFeatureFlagService featureFlagService,
        ILogger<GetUserAvailableFeaturesQueryHandler> _logger)
    {
        _userProfileReadService = userProfileReadService ?? throw new ArgumentNullException(nameof(userProfileReadService));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        this._logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
    }

    public async Task<IReadOnlyList<UserFeatureDto>> Handle(
        GetUserAvailableFeaturesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get user profile
        var userProfile = await _userProfileReadService.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        if (userProfile == null)
        {
            throw new NotFoundException($"User {query.UserId} not found");
        }

        // Parse role and tier from string values
        var userRole = ParseUserRole(userProfile.Role);
        var userTier = ParseUserTier(userProfile.Tier);

        // Get all feature flags
        var allFeatures = await _featureFlagService.GetAllFeatureFlagsAsync().ConfigureAwait(false);

        _logger.LogInformation(
            "Checking feature access for user {UserId} (role={Role}, tier={Tier}) across {Count} features",
            userProfile.Id, userProfile.Role, userProfile.Tier, allFeatures.Count);

        // Check access for each feature
        var result = new List<UserFeatureDto>();

        foreach (var feature in allFeatures)
        {
            bool hasAccess;

            // Admin users bypass all tier-based restrictions
            if (userRole == UserRole.Admin || userRole == UserRole.SuperAdmin)
            {
                hasAccess = true;
            }
            else
            {
                var roleAccess = await _featureFlagService.IsEnabledAsync(feature.FeatureName, userRole).ConfigureAwait(false);
                hasAccess = roleAccess && await _featureFlagService.IsEnabledForTierAsync(feature.FeatureName, userTier).ConfigureAwait(false);
            }

            var accessReason = hasAccess
                ? DetermineAccessReason(userProfile, feature)
                : "Access denied based on role or tier";

            result.Add(new UserFeatureDto
            {
                Key = feature.FeatureName,
                Name = FormatFeatureName(feature.FeatureName),
                Description = feature.Description,
                HasAccess = hasAccess,
                AccessReason = accessReason
            });
        }

        _logger.LogInformation(
            "User {UserId} has access to {AccessCount}/{TotalCount} features",
            userProfile.Id, result.Count(f => f.HasAccess), result.Count);

        return result;
    }

    private static string DetermineAccessReason(UserProfileDto userProfile, Models.FeatureFlagDto feature)
    {
        // Check if feature has tier restriction
        if (feature.TierRestriction != null)
        {
            return $"tier: {userProfile.Tier}";
        }

        // Check if feature has role restriction
        if (feature.RoleRestriction != null)
        {
            return $"role: {userProfile.Role}";
        }

        // Global feature
        return "available to all users";
    }

    private static string FormatFeatureName(string featureName)
    {
        // Convert snake_case or dot.notation to Title Case
        // e.g., "advanced_rag" → "Advanced Rag", "Features.Export" → "Features Export"
        return string.Join(" ",
            featureName.Replace('.', ' ')
               .Replace('_', ' ')
               .Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Select(word => word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant() : word));
    }

    private static UserRole ParseUserRole(string role) =>
        role.ToLowerInvariant() switch
        {
            "admin" => UserRole.Admin,
            "superadmin" => UserRole.SuperAdmin,
            "editor" => UserRole.Editor,
            "creator" => UserRole.Creator,
            _ => UserRole.User
        };

    private static UserTier ParseUserTier(string tier)
    {
        try { return UserTier.Parse(tier); }
        catch { return UserTier.Free; }
    }
}
