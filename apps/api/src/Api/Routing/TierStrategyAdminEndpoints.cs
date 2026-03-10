using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TierStrategy;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for tier-strategy configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
/// <remarks>
/// Provides endpoints for:
/// - Viewing the tier-strategy access matrix
/// - Configuring tier access to strategies
/// - Managing strategy-to-model mappings
/// - Resetting configuration to defaults
/// </remarks>
internal static class TierStrategyAdminEndpoints
{
    public static RouteGroupBuilder MapTierStrategyAdminEndpoints(this RouteGroupBuilder group)
    {
        var tierStrategyGroup = group.MapGroup("/admin/tier-strategy")
            .WithTags("Admin - Tier Strategy Configuration");

        // GET /api/v1/admin/tier-strategy/matrix - Get tier-strategy access matrix
        tierStrategyGroup.MapGet("/matrix", HandleGetMatrix)
            .WithName("GetTierStrategyMatrix")
            .WithSummary("Get tier-strategy access matrix")
            .WithDescription("Returns the complete matrix of tier-strategy access configurations. Shows which user tiers can access which RAG strategies.")
            .Produces<TierStrategyMatrixDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/tier-strategy/model-mappings - Get strategy-model mappings
        tierStrategyGroup.MapGet("/model-mappings", HandleGetModelMappings)
            .WithName("GetStrategyModelMappings")
            .WithSummary("Get strategy-model mappings")
            .WithDescription("Returns all strategy-to-model mapping configurations. Shows which LLM models are used for each RAG strategy.")
            .Produces<IReadOnlyList<StrategyModelMappingDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/tier-strategy/access - Update tier-strategy access
        tierStrategyGroup.MapPut("/access", HandleUpdateAccess)
            .WithName("UpdateTierStrategyAccess")
            .WithSummary("Update tier-strategy access")
            .WithDescription("Updates whether a specific user tier can access a specific RAG strategy.")
            .Produces<TierStrategyAccessDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/tier-strategy/model-mapping - Update strategy-model mapping
        tierStrategyGroup.MapPut("/model-mapping", HandleUpdateModelMapping)
            .WithName("UpdateStrategyModelMapping")
            .WithSummary("Update strategy-model mapping")
            .WithDescription("Updates the LLM model configuration for a specific RAG strategy.")
            .Produces<StrategyModelMappingDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/tier-strategy/apply-replacement - Apply model replacement
        tierStrategyGroup.MapPost("/apply-replacement", HandleApplyReplacement)
            .WithName("ApplyModelReplacement")
            .WithSummary("Apply model replacement across affected strategies")
            .WithDescription("Replaces a deprecated/unavailable model with a suggested replacement across all strategy mappings that use it as primary model. Issue #5499.")
            .Produces<ApplyModelReplacementResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/tier-strategy/model-health - Get model health/compatibility
        tierStrategyGroup.MapGet("/model-health", HandleGetModelHealth)
            .WithName("GetModelHealth")
            .WithSummary("Get model health and availability status")
            .WithDescription("Returns health/availability data for all tracked LLM models. Issue #5503.")
            .Produces<GetModelHealthResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/tier-strategy/model-change-history - Get model change audit log
        tierStrategyGroup.MapGet("/model-change-history", HandleGetModelChangeHistory)
            .WithName("GetModelChangeHistory")
            .WithSummary("Get model change history")
            .WithDescription("Returns audit trail of model changes (swaps, deprecations, fallbacks). Issue #5503.")
            .Produces<GetModelChangeHistoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/tier-strategy/check-now - Trigger immediate availability check
        tierStrategyGroup.MapPost("/check-now", HandleCheckNow)
            .WithName("TriggerModelAvailabilityCheck")
            .WithSummary("Trigger immediate model availability check")
            .WithDescription("Triggers the ModelAvailabilityCheckJob to run immediately. Issue #5503.")
            .Produces<TriggerModelAvailabilityCheckResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/tier-strategy/reset - Reset to defaults
        tierStrategyGroup.MapPost("/reset", HandleReset)
            .WithName("ResetTierStrategyConfig")
            .WithSummary("Reset tier-strategy configuration to defaults")
            .WithDescription("Deletes all custom tier-strategy access and model mapping configurations, reverting to system defaults.")
            .Produces<TierStrategyResetResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return group;
    }

    // ========================================
    // Handler Methods
    // ========================================

    private static async Task<IResult> HandleGetMatrix(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetTierStrategyMatrixQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetModelMappings(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetStrategyModelMappingsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateAccess(
        [FromBody] UpdateTierStrategyAccessRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} updating tier-strategy access: {Tier}/{Strategy} = {IsEnabled}",
            session!.User!.Id,
            request.Tier,
            request.Strategy,
            request.IsEnabled);

        var command = new UpdateTierStrategyAccessCommand(
            request.Tier,
            request.Strategy,
            request.IsEnabled);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateModelMapping(
        [FromBody] UpdateStrategyModelMappingRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} updating strategy-model mapping: {Strategy} -> {Provider}/{Model}",
            session!.User!.Id,
            request.Strategy,
            request.Provider,
            request.PrimaryModel);

        var command = new UpdateStrategyModelMappingCommand(
            request.Strategy,
            request.Provider,
            request.PrimaryModel,
            request.FallbackModels);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleApplyReplacement(
        [FromBody] ApplyModelReplacementRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} applying model replacement: {DeprecatedModel} → {ReplacementModel}",
            session!.User!.Id,
            request.DeprecatedModelId,
            request.ReplacementModelId);

        var command = new ApplyModelReplacementCommand(
            request.DeprecatedModelId,
            request.ReplacementModelId);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetModelHealth(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetModelHealthQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetModelChangeHistory(
        [FromQuery] string? modelId,
        [FromQuery] int? limit,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var clampedLimit = Math.Clamp(limit ?? 50, 1, 500);
        var query = new GetModelChangeHistoryQuery(modelId, clampedLimit);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCheckNow(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} triggered immediate model availability check",
            session!.User!.Id);

        var command = new TriggerModelAvailabilityCheckCommand();
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleReset(
        [FromBody] ResetTierStrategyConfigRequest? request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogWarning(
            "Admin {AdminId} resetting tier-strategy configuration: AccessMatrix={ResetAccess}, ModelMappings={ResetMappings}",
            session!.User!.Id,
            request?.ResetAccessMatrix ?? true,
            request?.ResetModelMappings ?? true);

        var command = new ResetTierStrategyConfigCommand(
            request?.ResetAccessMatrix ?? true,
            request?.ResetModelMappings ?? true);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

// ========================================
// Request DTOs
// ========================================

/// <summary>
/// Request DTO for updating tier-strategy access.
/// </summary>
internal sealed record UpdateTierStrategyAccessRequest(
    string Tier,
    string Strategy,
    bool IsEnabled
);

/// <summary>
/// Request DTO for updating strategy-model mapping.
/// </summary>
internal sealed record UpdateStrategyModelMappingRequest(
    string Strategy,
    string Provider,
    string PrimaryModel,
    IReadOnlyList<string>? FallbackModels = null
);

/// <summary>
/// Request DTO for applying a model replacement.
/// Issue #5499.
/// </summary>
internal sealed record ApplyModelReplacementRequest(
    string DeprecatedModelId,
    string ReplacementModelId
);

/// <summary>
/// Request DTO for resetting tier-strategy configuration.
/// </summary>
internal sealed record ResetTierStrategyConfigRequest(
    bool ResetAccessMatrix = true,
    bool ResetModelMappings = true
);
