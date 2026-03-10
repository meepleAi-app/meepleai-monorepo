using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Issue #5495: Admin endpoints for LLM system configuration management.
/// Provides GET/PUT for circuit breaker, budget, and fallback chain settings.
/// </summary>
internal static class AdminLlmConfigEndpoints
{
    public static void MapAdminLlmConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/llm/config")
            .WithTags("Admin - LLM Configuration")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/llm/config
        group.MapGet("/", GetConfig)
            .WithName("GetLlmSystemConfig")
            .WithSummary("Get current LLM system configuration")
            .WithDescription("Returns DB-stored config if present, otherwise appsettings.json defaults.");

        // PUT /api/v1/admin/llm/config
        group.MapPut("/", UpdateConfig)
            .WithName("UpdateLlmSystemConfig")
            .WithSummary("Update LLM system configuration")
            .WithDescription("Upserts circuit breaker thresholds, budget limits, and fallback chain. Takes effect within 60 seconds (cache TTL).");
    }

    private static async Task<IResult> GetConfig(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var config = await mediator.Send(new GetLlmSystemConfigQuery(), ct).ConfigureAwait(false);
        return Results.Ok(config);
    }

    private static async Task<IResult> UpdateConfig(
        [FromBody] UpdateLlmSystemConfigRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var adminUserId = GetAdminUserId(httpContext);
        if (adminUserId == Guid.Empty)
            return Results.Unauthorized();

        var result = await mediator.Send(new UpdateLlmSystemConfigCommand(
            CircuitBreakerFailureThreshold: request.CircuitBreakerFailureThreshold,
            CircuitBreakerOpenDurationSeconds: request.CircuitBreakerOpenDurationSeconds,
            CircuitBreakerSuccessThreshold: request.CircuitBreakerSuccessThreshold,
            DailyBudgetUsd: request.DailyBudgetUsd,
            MonthlyBudgetUsd: request.MonthlyBudgetUsd,
            FallbackChainJson: request.FallbackChainJson,
            UpdatedByUserId: adminUserId), ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static Guid GetAdminUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst("sub")
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            return userId;

        if (context.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid id)
            return id;

        return Guid.Empty;
    }
}

/// <summary>
/// Request body for updating LLM system configuration.
/// </summary>
internal record UpdateLlmSystemConfigRequest(
    int CircuitBreakerFailureThreshold,
    int CircuitBreakerOpenDurationSeconds,
    int CircuitBreakerSuccessThreshold,
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    string FallbackChainJson);
