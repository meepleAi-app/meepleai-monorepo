using Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Issue #5476: Admin endpoints for LLM emergency operational controls.
/// Provides immediate remediation actions during incidents.
/// </summary>
internal static class AdminEmergencyControlsEndpoints
{
    public static void MapAdminEmergencyControlsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/llm/emergency")
            .WithTags("Admin - LLM Emergency Controls")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /api/v1/admin/llm/emergency/activate
        group.MapPost("/activate", ActivateOverride)
            .WithName("ActivateEmergencyOverride")
            .WithSummary("Activate an emergency operational override")
            .WithDescription(@"
Activate an emergency override for the LLM subsystem. Actions:
- force-ollama-only: Route all traffic to Ollama (no OpenRouter)
- reset-circuit-breaker: Reset circuit breaker state for a provider
- flush-quota-cache: Clear free model quota cache in Redis

All actions auto-revert after the specified duration (default 30min, max 24h).
All actions are audit-logged with admin ID, reason, and timestamp.
");

        // POST /api/v1/admin/llm/emergency/deactivate
        group.MapPost("/deactivate", DeactivateOverride)
            .WithName("DeactivateEmergencyOverride")
            .WithSummary("Deactivate an active emergency override (early manual revert)")
            .WithDescription("Removes an active override before its TTL expires.");

        // GET /api/v1/admin/llm/emergency/active
        group.MapGet("/active", GetActiveOverrides)
            .WithName("GetActiveEmergencyOverrides")
            .WithSummary("Get all currently active emergency overrides")
            .WithDescription("Returns a list of active overrides with remaining duration.");
    }

    private static async Task<IResult> ActivateOverride(
        [FromBody] ActivateEmergencyOverrideRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var adminUserId = httpContext.GetAdminUserId();
        if (adminUserId == Guid.Empty)
            return Results.Unauthorized();

        var result = await mediator.Send(new ActivateEmergencyOverrideCommand(
            Action: request.Action,
            DurationMinutes: request.DurationMinutes ?? 30,
            Reason: request.Reason,
            AdminUserId: adminUserId,
            TargetProvider: request.TargetProvider), ct).ConfigureAwait(false);

        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> DeactivateOverride(
        [FromBody] DeactivateEmergencyOverrideRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var adminUserId = httpContext.GetAdminUserId();
        if (adminUserId == Guid.Empty)
            return Results.Unauthorized();

        var result = await mediator.Send(new DeactivateEmergencyOverrideCommand(
            Action: request.Action,
            AdminUserId: adminUserId), ct).ConfigureAwait(false);

        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> GetActiveOverrides(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var overrides = await mediator.Send(
            new GetActiveEmergencyOverridesQuery(), ct).ConfigureAwait(false);
        return Results.Ok(overrides);
    }

    /// <summary>
    /// Helper to extract admin user ID from HttpContext.
    /// </summary>
    private static Guid GetAdminUserId(this HttpContext context)
    {
        var userIdClaim = context.User.FindFirst("sub")
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            return userId;

        // Fallback: check Items dictionary (set by RequireAdminSessionFilter)
        if (context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid id)
            return id;

        return Guid.Empty;
    }
}

/// <summary>
/// Request body for activating an emergency override.
/// </summary>
internal record ActivateEmergencyOverrideRequest(
    string Action,
    int? DurationMinutes,
    string Reason,
    string? TargetProvider = null);

/// <summary>
/// Request body for deactivating an emergency override.
/// </summary>
internal record DeactivateEmergencyOverrideRequest(string Action);
