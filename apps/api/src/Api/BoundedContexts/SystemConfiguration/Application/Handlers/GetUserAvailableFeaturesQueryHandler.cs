using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using NotFoundException = Api.Middleware.Exceptions.NotFoundException;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of features available to a specific user.
/// Combines role and tier checks to determine access.
/// Issue #3674: Feature Flags Verification - User features endpoint
/// </summary>
internal sealed class GetUserAvailableFeaturesQueryHandler : IQueryHandler<GetUserAvailableFeaturesQuery, IReadOnlyList<UserFeatureDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<GetUserAvailableFeaturesQueryHandler> _logger;

    public GetUserAvailableFeaturesQueryHandler(
        IUserRepository userRepository,
        IFeatureFlagService featureFlagService,
        ILogger<GetUserAvailableFeaturesQueryHandler> _logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        this._logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
    }

    public async Task<IReadOnlyList<UserFeatureDto>> Handle(
        GetUserAvailableFeaturesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get user
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new NotFoundException($"User {query.UserId} not found");
        }

        // Get all feature flags
        var allFeatures = await _featureFlagService.GetAllFeatureFlagsAsync().ConfigureAwait(false);

        _logger.LogInformation(
            "Checking feature access for user {UserId} (role={Role}, tier={Tier}) across {Count} features",
            user.Id, user.Role, user.Tier, allFeatures.Count);

        // Check access for each feature
        var result = new List<UserFeatureDto>();

        foreach (var feature in allFeatures)
        {
            var hasAccess = await _featureFlagService.CanAccessFeatureAsync(user, feature.FeatureName).ConfigureAwait(false);

            var accessReason = hasAccess
                ? DetermineAccessReason(user, feature)
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
            user.Id, result.Count(f => f.HasAccess), result.Count);

        return result;
    }

    private static string DetermineAccessReason(Api.BoundedContexts.Authentication.Domain.Entities.User user, Models.FeatureFlagDto feature)
    {
        // Check if feature has tier restriction
        if (feature.TierRestriction != null)
        {
            return $"tier: {user.Tier.Value}";
        }

        // Check if feature has role restriction
        if (feature.RoleRestriction != null)
        {
            return $"role: {user.Role.Value}";
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
}
