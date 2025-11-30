using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// System configuration management endpoints (Admin only).
/// Handles CRUD operations, validation, versioning, import/export, and cache management.
/// </summary>
public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
    {
        // CONFIG-01: Configuration management endpoints (Admin only) - MIGRATED TO CQRS
        group.MapGet("/admin/configurations", async (
            HttpContext context,
            IMediator mediator,
            string? category = null,
            string? environment = null,
            bool activeOnly = true,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAllConfigsQuery(category, environment, activeOnly, page, pageSize);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(result);
        })
        .WithName("GetConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<PagedConfigurationResult>();

        group.MapGet("/admin/configurations/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigByIdQuery(id);
            var config = await mediator.Send(query, ct).ConfigureAwait(false);
            return config != null ? Results.Json(config) : Results.NotFound();
        })
        .WithName("GetConfigurationById")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/key/{key}", async (
            string key,
            HttpContext context,
            IMediator mediator,
            string? environment = null,
            bool activeOnly = false,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigByKeyQuery(key, environment, activeOnly);
            var config = await mediator.Send(query, ct).ConfigureAwait(false);
            return config != null ? Results.Json(config) : Results.NotFound();
        })
        .WithName("GetConfigurationByKey")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations", async (
            CreateConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating configuration {Key}", session.User.Id, request.Key);
            var command = new CreateConfigurationCommand(
                Key: request.Key,
                Value: request.Value,
                ValueType: request.ValueType,
                CreatedByUserId: Guid.Parse(session.User.Id),
                Description: request.Description,
                Category: request.Category,
                Environment: request.Environment,
                RequiresRestart: request.RequiresRestart
            );
            var config = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Configuration {Key} created with ID {Id}", request.Key, config.Id);
            return Results.Created($"/api/v1/admin/configurations/{config.Id}", config);
        })
        .WithName("CreateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>(201)
        .ProducesValidationProblem();

        group.MapPut("/admin/configurations/{id:guid}", async (
            Guid id,
            UpdateConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} updating configuration {Id}", session.User.Id, id);

            // For simplicity, we only support value updates via this endpoint
            if (request.Value == null)
            {
                return Results.BadRequest(new { error = "Value is required for update" });
            }

            var command = new UpdateConfigValueCommand(
                ConfigId: id,
                NewValue: request.Value,
                UpdatedByUserId: Guid.Parse(session.User.Id)
            );
            var config = await mediator.Send(command, ct).ConfigureAwait(false);

            if (config == null)
            {
                logger.LogWarning("Configuration {Id} not found for update", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} updated to version {Version}", id, config.Version);
            return Results.Json(config);
        })
        .WithName("UpdateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404)
        .ProducesValidationProblem();

        group.MapDelete("/admin/configurations/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} deleting configuration {Id}", session.User.Id, id);
            var command = new DeleteConfigurationCommand(id);
            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!success)
            {
                logger.LogWarning("Configuration {Id} not found for deletion", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} deleted successfully", id);
            return Results.NoContent();
        })
        .WithName("DeleteConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces(204)
        .Produces(404);

        group.MapPatch("/admin/configurations/{id:guid}/toggle", async (
            Guid id,
            bool isActive,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} toggling configuration {Id} to {Status}",
                session.User.Id, id, isActive ? "active" : "inactive");

            var command = new ToggleConfigurationCommand(id, isActive);
            var config = await mediator.Send(command, ct).ConfigureAwait(false);

            if (config == null)
            {
                logger.LogWarning("Configuration {Id} not found for toggle", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} toggled to {Status}", id, config.IsActive ? "active" : "inactive");
            return Results.Json(config);
        })
        .WithName("ToggleConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations/bulk-update", async (
            BulkConfigurationUpdateRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} performing bulk update on {Count} configurations",
                session.User.Id, request.Updates.Count);

            var updates = request.Updates.Select(u => new BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationUpdate(
                Id: Guid.Parse(u.Id),
                Value: u.Value
            )).ToList();

            var command = new BulkUpdateConfigsCommand(updates, Guid.Parse(session.User.Id));
            var configs = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Bulk update completed successfully for {Count} configurations", configs.Count);
            return Results.Json(configs);
        })
        .WithName("BulkUpdateConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<ConfigurationDto>>()
        .ProducesValidationProblem();

        group.MapPost("/admin/configurations/validate", async (
            string key,
            string value,
            string valueType,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new ValidateConfigCommand(key, value, valueType);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Json(result);
        })
        .WithName("ValidateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationValidationResult>();

        group.MapGet("/admin/configurations/export", async (
            string environment,
            HttpContext context,
            IMediator mediator,
            bool activeOnly = true,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new ExportConfigsQuery(environment, activeOnly);
            var export = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(export);
        })
        .WithName("ExportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationExportDto>();

        group.MapPost("/admin/configurations/import", async (
            ConfigurationImportRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} importing {Count} configurations",
                session.User.Id, request.Configurations.Count);

            var items = request.Configurations.Select(c => new ConfigurationImportItem(
                Key: c.Key,
                Value: c.Value,
                ValueType: c.ValueType,
                Description: c.Description,
                Category: c.Category,
                IsActive: c.IsActive,
                RequiresRestart: c.RequiresRestart,
                Environment: c.Environment
            )).ToList();

            var command = new ImportConfigsCommand(items, request.OverwriteExisting, Guid.Parse(session.User.Id));
            var importedCount = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Successfully imported {Count} configurations", importedCount);
            return Results.Json(new { importedCount });
        })
        .WithName("ImportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<object>()
        .ProducesValidationProblem();

        group.MapGet("/admin/configurations/{id:guid}/history", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigHistoryQuery(id, limit);
            var history = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(history);
        })
        .WithName("GetConfigurationHistory")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationHistoryDto>>();

        group.MapPost("/admin/configurations/{id:guid}/rollback/{version:int}", async (
            Guid id,
            int version,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} rolling back configuration {Id} to version {Version}",
                session.User.Id, id, version);

            var command = new RollbackConfigCommand(id, version, Guid.Parse(session.User.Id));
            var config = await mediator.Send(command, ct).ConfigureAwait(false);

            if (config == null)
            {
                logger.LogWarning("Configuration {Id} not found for rollback", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} rolled back successfully", id);
            return Results.Json(config);
        })
        .WithName("RollbackConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/categories", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigCategoriesQuery();
            var categories = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(categories);
        })
        .WithName("GetCategories")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<string>>();

        group.MapPost("/admin/configurations/cache/invalidate", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            string? key = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (key != null)
            {
                logger.LogInformation("Admin {AdminId} invalidating cache for configuration key {Key}", session.User.Id, key);
            }
            else
            {
                logger.LogInformation("Admin {AdminId} invalidating all configuration cache", session.User.Id);
            }

            var command = new InvalidateCacheCommand(key);
            await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Json(new { ok = true, message = key != null ? $"Cache invalidated for key: {key}" : "All configuration cache invalidated" });
        })
        .WithName("InvalidateConfigurationCache")
        .WithTags("Admin", "Configuration")
        .Produces<object>();

        return group;
    }
}
