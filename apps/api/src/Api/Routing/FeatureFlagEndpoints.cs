using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using Api.Services;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// Feature flag management endpoints (Admin only).
/// Feature flags are stored as configurations with category "Features".
/// Provides a simplified interface for toggling and querying feature flags.
/// </summary>
internal static class FeatureFlagEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagEndpoints(this RouteGroupBuilder group)
    {
        MapListFeatureFlagsEndpoint(group);
        MapGetFeatureFlagEndpoint(group);
        MapToggleFeatureFlagEndpoint(group);
        MapCreateFeatureFlagEndpoint(group);

        return group;
    }

    private static void MapListFeatureFlagsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/feature-flags", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Query configurations with "Features" category
            var query = new GetAllConfigsQuery(
                Category: "Features",
                Environment: null,
                ActiveOnly: false,
                Page: 1,
                PageSize: 100
            );
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(new
            {
                featureFlags = result.Items.Select(c => new
                {
                    key = c.Key,
                    enabled = c.IsActive && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase),
                    description = c.Description,
                    lastModified = c.UpdatedAt,
                    version = c.Version
                }),
                totalCount = result.Total
            });
        })
        .WithName("ListFeatureFlags")
        .WithTags("Admin", "FeatureFlags")
        .WithDescription("List all feature flags (configurations with 'Features' category)")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapGetFeatureFlagEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/feature-flags/{key}", async (
            string key,
            HttpContext context,
            IFeatureFlagService featureFlags,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Use FeatureFlagService to check current status
            var isEnabled = await featureFlags.IsEnabledAsync(key).ConfigureAwait(false);

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
