using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// AI Model administration endpoints (Admin only).
/// Handles AI model CRUD operations, priority management, and activation control.
/// </summary>
/// <remarks>
/// Issue #2567: Complete HTTP API layer for AI model management (Issue #2520)
/// </remarks>
internal static class AiModelAdminEndpoints
{
    public static RouteGroupBuilder MapAiModelAdminEndpoints(this RouteGroupBuilder group)
    {
        var aiModelsGroup = group.MapGroup("/admin/ai-models")
            .WithTags("Admin - AI Models");

        // GET /api/v1/admin/ai-models - List all AI models
        aiModelsGroup.MapGet("/", HandleGetAllModels)
            .WithName("GetAllAiModels")
            .WithSummary("Get all AI model configurations")
            .WithDescription("Returns all AI models with optional filtering by provider and active status. Results are ordered by priority (lower = higher preference).")
            .Produces<AiModelListDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/ai-models/{id} - Get single AI model by ID
        aiModelsGroup.MapGet("/{id:guid}", HandleGetModelById)
            .WithName("GetAiModelById")
            .WithSummary("Get AI model by ID")
            .WithDescription("Returns a single AI model configuration by its unique identifier.")
            .Produces<AiModelDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/ai-models - Create new AI model
        aiModelsGroup.MapPost("/", HandleCreateModel)
            .WithName("CreateAiModel")
            .WithSummary("Create new AI model configuration")
            .WithDescription("Creates a new AI model configuration with settings and pricing information.")
            .Produces<AiModelDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/ai-models/{id} - Update existing AI model
        aiModelsGroup.MapPut("/{id:guid}", HandleUpdateModel)
            .WithName("UpdateAiModel")
            .WithSummary("Update AI model configuration")
            .WithDescription("Updates an existing AI model configuration including settings, pricing, priority, and status.")
            .Produces<AiModelDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/v1/admin/ai-models/{id} - Delete AI model
        aiModelsGroup.MapDelete("/{id:guid}", HandleDeleteModel)
            .WithName("DeleteAiModel")
            .WithSummary("Delete AI model configuration")
            .WithDescription("Deletes an AI model configuration. Primary models cannot be deleted.")
            .Produces<bool>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // PATCH /api/v1/admin/ai-models/{id}/priority - Update model priority
        aiModelsGroup.MapPatch("/{id:guid}/priority", HandleUpdatePriority)
            .WithName("UpdateAiModelPriority")
            .WithSummary("Update AI model priority")
            .WithDescription("Updates the priority of an AI model. Lower priority = higher preference (1 = primary, 2 = first fallback, etc.).")
            .Produces<AiModelDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PATCH /api/v1/admin/ai-models/{id}/toggle - Toggle active status
        aiModelsGroup.MapPatch("/{id:guid}/toggle", HandleToggleActive)
            .WithName("ToggleAiModelActive")
            .WithSummary("Toggle AI model active status")
            .WithDescription("Toggles an AI model between active and inactive. Primary models cannot be deactivated.")
            .Produces<AiModelDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    // ========================================
    // Handler Methods
    // ========================================

    private static async Task<IResult> HandleGetAllModels(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        [FromQuery] string? provider = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllAiModelsQuery(provider, isActive, page, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetModelById(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAiModelByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateModel(
        CreateAiModelCommand command,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} creating AI model: {ModelId} ({Provider})",
            session!.User!.Id,
            command.ModelId,
            command.Provider
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/ai-models/{result.Id}", result);
    }

    private static async Task<IResult> HandleUpdateModel(
        Guid id,
        [FromBody] UpdateAiModelRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} updating AI model: {ModelId}",
            session!.User!.Id,
            id
        );

        var command = new UpdateAiModelCommand(
            id,
            request.Priority,
            request.IsActive,
            request.IsPrimary,
            request.Settings
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteModel(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} deleting AI model: {ModelId}",
            session!.User!.Id,
            id
        );

        var command = new DeleteAiModelCommand(id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(new { success = result });
    }

    private static async Task<IResult> HandleUpdatePriority(
        Guid id,
        [FromBody] UpdatePriorityRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} updating priority for AI model {ModelId} to {Priority}",
            session!.User!.Id,
            id,
            request.NewPriority
        );

        var command = new UpdateModelPriorityCommand(id, request.NewPriority);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleToggleActive(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} toggling active status for AI model: {ModelId}",
            session!.User!.Id,
            id
        );

        var command = new ToggleAiModelActiveCommand(id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

// ========================================
// Request DTOs
// ========================================

/// <summary>
/// Request DTO for updating AI model
/// Note: ModelId and DisplayName are immutable and cannot be updated
/// </summary>
internal sealed record UpdateAiModelRequest(
    int Priority,
    bool IsActive,
    bool IsPrimary,
    ModelSettings Settings
);

/// <summary>
/// Request DTO for updating model priority
/// </summary>
internal sealed record UpdatePriorityRequest(int NewPriority);
