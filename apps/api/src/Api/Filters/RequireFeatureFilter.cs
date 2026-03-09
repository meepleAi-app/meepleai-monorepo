using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that gates access based on feature flag status.
/// Checks both role-based and tier-based access for the authenticated user.
///
/// Usage: Apply via .RequireFeature("feature_name") fluent method on route endpoints.
///
/// Pattern:
/// - Requires an active session (must be used after .RequireSession())
/// - Checks feature flag access using FeatureFlagService.CanAccessFeatureAsync()
/// - Returns 403 Forbidden if user doesn't have access to the feature
/// - Returns 401 Unauthorized if session is not available
///
/// Example:
/// <code>
/// group.MapGet("/advanced-search", handler)
///     .RequireSession()
///     .RequireFeature("advanced_rag");
/// </code>
///
/// Issue: #3674 - Feature Flag Tier-Based Access Verification
/// </summary>
internal class RequireFeatureFilter : IEndpointFilter
{
    private readonly string _featureName;

    public RequireFeatureFilter(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        _featureName = featureName;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Get session from HttpContext.Items (set by RequireSessionFilter)
        var session = httpContext.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        if (session?.User == null)
        {
            return Results.Json(
                new { error = "Authentication required to access this feature" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Resolve feature flag service from DI
        var featureFlagService = httpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireFeatureFilter>>();

        // Get the full user entity for combined role+tier check
        var user = await userRepository.GetByIdAsync(session.User.Id).ConfigureAwait(false);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found during feature check for {Feature}",
                session.User.Id, _featureName);
            return Results.Json(
                new { error = "User not found" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Check combined role + tier access
        var hasAccess = await featureFlagService.CanAccessFeatureAsync(user, _featureName).ConfigureAwait(false);

        if (!hasAccess)
        {
            logger.LogInformation(
                "Feature {Feature} access denied for user {UserId} (role={Role}, tier={Tier})",
                _featureName, user.Id, user.Role, user.Tier);

            return Results.Json(
                new
                {
                    error = $"Access to feature '{_featureName}' is not available for your current plan",
                    feature = _featureName,
                    requiredUpgrade = true
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        logger.LogDebug("Feature {Feature} access granted for user {UserId}", _featureName, user.Id);
        return await next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Factory for creating RequireFeatureFilter instances with a specific feature name.
/// Used by the RequireFeature extension method.
/// </summary>
internal class RequireFeatureFilterFactory : IEndpointFilter
{
    private readonly string _featureName;

    public RequireFeatureFilterFactory(string featureName)
    {
        _featureName = featureName;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var filter = new RequireFeatureFilter(_featureName);
        return filter.InvokeAsync(context, next);
    }
}
