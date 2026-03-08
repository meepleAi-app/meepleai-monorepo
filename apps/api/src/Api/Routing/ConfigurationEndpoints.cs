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
internal static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
    {
        // CONFIG-01: Configuration management endpoints (Admin only) - MIGRATED TO CQRS
        MapConfigurationCrudEndpoints(group);
        MapConfigurationMaintenanceEndpoints(group);

        return group;
    }

    private static void MapConfigurationCrudEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/admin/configurations", HandleGetConfigurations)
        .WithName("GetConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<PagedConfigurationResult>();

        group.MapGet("/admin/configurations/{id:guid}", HandleGetConfigurationById)
        .WithName("GetConfigurationById")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/key/{key}", HandleGetConfigurationByKey)
        .WithName("GetConfigurationByKey")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations", HandleCreateConfiguration)
        .WithName("CreateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>(201)
        .ProducesValidationProblem();

        group.MapPut("/admin/configurations/{id:guid}", HandleUpdateConfiguration)
        .WithName("UpdateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404)
        .ProducesValidationProblem();

        group.MapDelete("/admin/configurations/{id:guid}", HandleDeleteConfiguration)
        .WithName("DeleteConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces(204)
        .Produces(404);

        group.MapPatch("/admin/configurations/{id:guid}/toggle", HandleToggleConfiguration)
        .WithName("ToggleConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/categories", HandleGetCategories)
        .WithName("GetCategories")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<string>>();
    }

    private static void MapConfigurationMaintenanceEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/admin/configurations/bulk-update", HandleBulkUpdateConfigurations)
        .WithName("BulkUpdateConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<ConfigurationDto>>()
        .ProducesValidationProblem();

        group.MapPost("/admin/configurations/validate", HandleValidateConfiguration)
        .WithName("ValidateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationValidationResult>();

        group.MapGet("/admin/configurations/export", HandleExportConfigurations)
        .WithName("ExportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationExportDto>();

        group.MapPost("/admin/configurations/import", HandleImportConfigurations)
        .WithName("ImportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<object>()
        .ProducesValidationProblem();

        group.MapGet("/admin/configurations/{id:guid}/history", HandleGetConfigurationHistory)
        .WithName("GetConfigurationHistory")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationHistoryDto>>();

        group.MapPost("/admin/configurations/{id:guid}/rollback/{version:int}", HandleRollbackConfiguration)
        .WithName("RollbackConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations/cache/invalidate", HandleInvalidateCache)
        .WithName("InvalidateConfigurationCache")
        .WithTags("Admin", "Configuration")
        .Produces<object>();
    }

    private static async Task<IResult> HandleGetConfigurations(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? category = null,
        string? environment = null,
        bool activeOnly = true,
        int page = 1,
        int pageSize = 50)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllConfigsQuery(category, environment, activeOnly, page, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Json(result);
    }

    private static async Task<IResult> HandleGetConfigurationById(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetConfigByIdQuery(id);
        var config = await mediator.Send(query, ct).ConfigureAwait(false);
        return config != null ? Results.Json(config) : Results.NotFound();
    }

    private static async Task<IResult> HandleGetConfigurationByKey(
        string key,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? environment = null,
        bool activeOnly = false)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetConfigByKeyQuery(key, environment, activeOnly);
        var config = await mediator.Send(query, ct).ConfigureAwait(false);
        return config != null ? Results.Json(config) : Results.NotFound();
    }

    private static async Task<IResult> HandleCreateConfiguration(
        CreateConfigurationRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} creating configuration {Key}", session!.User!.Id, request.Key);
        var command = new CreateConfigurationCommand(
            Key: request.Key,
            Value: request.Value,
            ValueType: request.ValueType,
            CreatedByUserId: session!.User!.Id,
            Description: request.Description,
            Category: request.Category,
            Environment: request.Environment,
            RequiresRestart: request.RequiresRestart
        );
        var config = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Configuration {Key} created with ID {Id}", request.Key, config.Id);
        return Results.Created($"/api/v1/admin/configurations/{config.Id}", config);
    }

    private static async Task<IResult> HandleUpdateConfiguration(
        Guid id,
        UpdateConfigurationRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} updating configuration {Id}", session!.User!.Id, id);

        // For simplicity, we only support value updates via this endpoint
        if (request.Value == null)
        {
            return Results.BadRequest(new { error = "Value is required for update" });
        }

        var command = new UpdateConfigValueCommand(
            ConfigId: id,
            NewValue: request.Value,
            UpdatedByUserId: session!.User!.Id
        );
        var config = await mediator.Send(command, ct).ConfigureAwait(false);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for update", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} updated to version {Version}", id, config.Version);
        return Results.Json(config);
    }

    private static async Task<IResult> HandleDeleteConfiguration(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} deleting configuration {Id}", session!.User!.Id, id);
        var command = new DeleteConfigurationCommand(id);
        var success = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!success)
        {
            logger.LogWarning("Configuration {Id} not found for deletion", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} deleted successfully", id);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleToggleConfiguration(
        Guid id,
        bool isActive,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} toggling configuration {Id} to {Status}",
            session!.User!.Id, id, isActive ? "active" : "inactive");

        var command = new ToggleConfigurationCommand(id, isActive);
        var config = await mediator.Send(command, ct).ConfigureAwait(false);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for toggle", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} toggled to {Status}", id, config.IsActive ? "active" : "inactive");
        return Results.Json(config);
    }

    private static async Task<IResult> HandleGetCategories(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetConfigCategoriesQuery();
        var categories = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Json(categories);
    }

    private static async Task<IResult> HandleBulkUpdateConfigurations(
        BulkConfigurationUpdateRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} performing bulk update on {Count} configurations",
            session!.User!.Id, request.Updates.Count);

        var invalidIds = request.Updates.Where(u => !Guid.TryParse(u.Id, out _)).Select(u => u.Id).ToList();
        if (invalidIds.Count > 0)
            return Results.BadRequest(new { error = $"Invalid configuration IDs: {string.Join(", ", invalidIds)}" });

        var updates = request.Updates.Select(u => new BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationUpdate(
            Id: Guid.Parse(u.Id),
            Value: u.Value
        )).ToList();

        var command = new BulkUpdateConfigsCommand(updates, session!.User!.Id);
        var configs = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Bulk update completed successfully for {Count} configurations", configs.Count);
        return Results.Json(configs);
    }

    private static async Task<IResult> HandleValidateConfiguration(
        string key,
        string value,
        string valueType,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ValidateConfigCommand(key, value, valueType);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Json(result);
    }

    private static async Task<IResult> HandleExportConfigurations(
        string environment,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        bool activeOnly = true)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new ExportConfigsQuery(environment, activeOnly);
        var export = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Json(export);
    }

    private static async Task<IResult> HandleImportConfigurations(
        ConfigurationImportRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} importing {Count} configurations",
            session!.User!.Id, request.Configurations.Count);

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

        var command = new ImportConfigsCommand(items, request.OverwriteExisting, session!.User!.Id);
        var importedCount = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Successfully imported {Count} configurations", importedCount);
        return Results.Json(new { importedCount });
    }

    private static async Task<IResult> HandleGetConfigurationHistory(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        int limit = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetConfigHistoryQuery(id, limit);
        var history = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Json(history);
    }

    private static async Task<IResult> HandleRollbackConfiguration(
        Guid id,
        int version,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} rolling back configuration {Id} to version {Version}",
            session!.User!.Id, id, version);

        var command = new RollbackConfigCommand(id, version, session!.User!.Id);
        var config = await mediator.Send(command, ct).ConfigureAwait(false);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for rollback", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} rolled back successfully", id);
        return Results.Json(config);
    }

    private static async Task<IResult> HandleInvalidateCache(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct,
        string? key = null)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (key != null)
        {
            logger.LogInformation("Admin {AdminId} invalidating cache for configuration key {Key}", session!.User!.Id, key);
        }
        else
        {
            logger.LogInformation("Admin {AdminId} invalidating all configuration cache", session!.User!.Id);
        }

        var command = new InvalidateCacheCommand(key);
        await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Json(new { ok = true, message = key != null ? $"Cache invalidated for key: {key}" : "All configuration cache invalidated" });
    }
}
