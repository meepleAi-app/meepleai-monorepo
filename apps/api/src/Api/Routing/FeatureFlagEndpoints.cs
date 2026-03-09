using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// Feature flag management endpoints (Admin only).
/// Feature flags are stored as configurations with category "Features".
/// Provides a simplified interface for toggling and querying feature flags.
/// Issue #3073: Extended to support tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal static class FeatureFlagEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagEndpoints(this RouteGroupBuilder group)
    {
        MapListFeatureFlagsEndpoint(group);
        MapGetFeatureFlagEndpoint(group);
        MapToggleFeatureFlagEndpoint(group);
        MapCreateFeatureFlagEndpoint(group);
        MapUpdateFeatureFlagEndpoint(group);
        MapEnableFeatureForTierEndpoint(group);
        MapDisableFeatureForTierEndpoint(group);

        return group;
    }

    /// <summary>
    /// GET /admin/feature-flags - List all feature flags with role and tier configurations.
    /// Issue #3073: Extended to include tier configuration.
    /// </summary>
    private static void MapListFeatureFlagsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/feature-flags", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Use CQRS query for feature flags which includes tier information
            var flags = await mediator.Send(new GetAllFeatureFlagsQuery(), ct).ConfigureAwait(false);

            return Results.Json(new
            {
                featureFlags = flags.Select(f => new
                {
                    key = f.FeatureName,
                    enabled = f.IsEnabled,
                    roleRestriction = f.RoleRestriction,
                    tierRestriction = f.TierRestriction,
                    description = f.Description
                }),
                totalCount = flags.Count
            });
        })
        .WithName("ListFeatureFlags")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("List all feature flags with role and tier configurations")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapGetFeatureFlagEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/feature-flags/{key}", async (
            string key,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Use CQRS query to check current status
            var isEnabled = await mediator.Send(new IsFeatureEnabledQuery(key), ct).ConfigureAwait(false);

            // Get configuration details
            var query = new GetConfigByKeyQuery(key, Environment: null);
            var config = await mediator.Send(query, ct).ConfigureAwait(false);

            if (config == null)
            {
                return Results.NotFound(new { error = $"Feature flag '{key}' not found" });
            }

            return Results.Json(new
            {
                key = config.Key,
                enabled = isEnabled,
                description = config.Description,
                category = config.Category,
                lastModified = config.UpdatedAt,
                createdAt = config.CreatedAt,
                version = config.Version
            });
        })
        .WithName("GetFeatureFlag")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Get specific feature flag status and details")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapToggleFeatureFlagEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/feature-flags/{key}/toggle", async (
            string key,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            bool enabled = true,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} toggling feature flag '{Key}' to {Status}",
                session!.User!.Id, key, enabled ? "enabled" : "disabled");

            // Get the configuration for this feature flag
            var configQuery = new GetConfigByKeyQuery(key, Environment: null);
            var config = await mediator.Send(configQuery, ct).ConfigureAwait(false);

            if (config == null)
            {
                logger.LogWarning("Feature flag '{Key}' not found", key);
                return Results.NotFound(new { error = $"Feature flag '{key}' not found" });
            }

            // Update the value
            var updateCommand = new UpdateConfigValueCommand(
                ConfigId: config.Id,
                NewValue: enabled.ToString().ToLowerInvariant(),
                UpdatedByUserId: session!.User!.Id
            );
            var updatedConfig = await mediator.Send(updateCommand, ct).ConfigureAwait(false);

            // Also ensure it's active
            if (updatedConfig != null && !updatedConfig.IsActive)
            {
                var toggleCommand = new ToggleConfigurationCommand(config.Id, IsActive: true);
                updatedConfig = await mediator.Send(toggleCommand, ct).ConfigureAwait(false);
            }

            logger.LogInformation("Feature flag '{Key}' toggled to {Status}", key, enabled ? "enabled" : "disabled");

            return Results.Json(new
            {
                key = updatedConfig?.Key,
                enabled,
                message = $"Feature flag '{key}' {(enabled ? "enabled" : "disabled")} successfully"
            });
        })
        .WithName("ToggleFeatureFlag")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Toggle a feature flag on or off")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapCreateFeatureFlagEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/feature-flags", async (
            CreateFeatureFlagRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating feature flag '{Key}'", session!.User!.Id, request.Key);

            // Default to "All" if no environment specified
            var environment = request.Environment ?? "All";

            var command = new CreateConfigurationCommand(
                Key: request.Key,
                Value: request.Enabled.ToString().ToLowerInvariant(),
                ValueType: "bool",
                CreatedByUserId: session!.User!.Id,
                Description: request.Description,
                Category: "Features",
                Environment: environment,
                RequiresRestart: request.RequiresRestart
            );

            var config = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Feature flag '{Key}' created with ID {Id}", request.Key, config.Id);

            return Results.Created($"/api/v1/admin/feature-flags/{request.Key}", new
            {
                key = config.Key,
                enabled = request.Enabled,
                description = config.Description,
                id = config.Id
            });
        })
        .WithName("CreateFeatureFlag")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Create a new feature flag")
        .Produces<object>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// PUT /admin/feature-flags/{key} - Update feature flag with optional role and tier.
    /// Issue #3073: Added tier-based update support.
    /// </summary>
    private static void MapUpdateFeatureFlagEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/admin/feature-flags/{key}", async (
            string key,
            FeatureFlagUpdateRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var userId = session!.User!.Id.ToString();

            logger.LogInformation(
                "Admin {AdminId} updating feature flag '{Key}' to {Status} (role={Role}, tier={Tier})",
                session.User.Id, key, request.Enabled ? "enabled" : "disabled", request.Role, request.Tier);

            // Parse optional role
            UserRole? role = null;
            if (!string.IsNullOrWhiteSpace(request.Role) &&
                Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var parsedRole))
            {
                role = parsedRole;
            }

            // Parse optional tier
            UserTier? tier = null;
            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                tier = UserTier.Parse(request.Tier);
            }

            // Use CQRS command to update the feature flag
            var command = new UpdateFeatureFlagCommand(
                FeatureName: key,
                Enabled: request.Enabled,
                Role: role,
                Tier: tier,
                UserId: userId);

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Feature flag '{Key}' updated successfully", key);

            return Results.Json(new
            {
                key,
                enabled = request.Enabled,
                role = request.Role,
                tier = request.Tier,
                message = $"Feature flag '{key}' updated successfully"
            });
        })
        .WithName("UpdateFeatureFlag")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Update a feature flag with optional role and tier restrictions")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// POST /admin/feature-flags/{key}/tier/{tier}/enable - Enable feature for specific tier.
    /// Issue #3073: Added tier-based enable endpoint.
    /// </summary>
    private static void MapEnableFeatureForTierEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/feature-flags/{key}/tier/{tier}/enable", async (
            string key,
            string tier,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Validate tier
            UserTier userTier;
            try
            {
                userTier = UserTier.Parse(tier);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = $"Invalid tier '{tier}'. Valid tiers: free, normal, premium" });
            }

            var userId = session!.User!.Id.ToString();

            logger.LogInformation(
                "Admin {AdminId} enabling feature flag '{Key}' for tier {Tier}",
                session.User.Id, key, tier);

            // Use CQRS command to enable the feature for the tier
            var command = new EnableFeatureForTierCommand(key, userTier, userId);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Feature flag '{Key}' enabled for tier {Tier}", key, tier);

            return Results.Json(new
            {
                key,
                tier,
                enabled = true,
                message = $"Feature flag '{key}' enabled for tier '{tier}'"
            });
        })
        .WithName("EnableFeatureFlagForTier")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Enable a feature flag for a specific tier (free, normal, premium)")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// POST /admin/feature-flags/{key}/tier/{tier}/disable - Disable feature for specific tier.
    /// Issue #3073: Added tier-based disable endpoint.
    /// </summary>
    private static void MapDisableFeatureForTierEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/feature-flags/{key}/tier/{tier}/disable", async (
            string key,
            string tier,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Validate tier
            UserTier userTier;
            try
            {
                userTier = UserTier.Parse(tier);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = $"Invalid tier '{tier}'. Valid tiers: free, normal, premium" });
            }

            var userId = session!.User!.Id.ToString();

            logger.LogInformation(
                "Admin {AdminId} disabling feature flag '{Key}' for tier {Tier}",
                session.User.Id, key, tier);

            // Use CQRS command to disable the feature for the tier
            var command = new DisableFeatureForTierCommand(key, userTier, userId);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Feature flag '{Key}' disabled for tier {Tier}", key, tier);

            return Results.Json(new
            {
                key,
                tier,
                enabled = false,
                message = $"Feature flag '{key}' disabled for tier '{tier}'"
            });
        })
        .WithName("DisableFeatureFlagForTier")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("Disable a feature flag for a specific tier (free, normal, premium)")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

/// <summary>
/// Request model for creating a new feature flag.
/// </summary>
internal record CreateFeatureFlagRequest(
    string Key,
    bool Enabled,
    string? Description = null,
    string? Environment = null,
    bool RequiresRestart = false
);
