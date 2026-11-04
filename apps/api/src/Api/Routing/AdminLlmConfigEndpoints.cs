using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetLlmBudgetUsage;
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
            .WithDescription("Upserts budget caps, degrade threshold and quotas. Circuit breaker thresholds and fallback chain are optional and kept when absent: the runtime does not read them (#4169). Takes effect within 60 seconds (cache TTL).");

        // GET /api/v1/admin/llm/budget/usage — #4169: il consumo come lo vede il gate
        app.MapGroup("/admin/llm/budget")
            .WithTags("Admin - LLM Configuration")
            .AddEndpointFilter<RequireAdminSessionFilter>()
            .MapGet("/usage", GetBudgetUsage)
            .WithName("GetLlmBudgetUsage")
            .WithSummary("Get today's and this month's LLM spend, per budget class")
            .WithDescription("The spend the LLM gateway counts against the caps (cost log plus calls still open), split into answers, background and evaluations, and whether the degrade is active. available=false when the spend cannot be read.");
    }

    private static async Task<IResult> GetBudgetUsage(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var usage = await mediator.Send(new GetLlmBudgetUsageQuery(), ct).ConfigureAwait(false);
        return Results.Ok(usage);
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
            DailyBudgetUsd: request.DailyBudgetUsd,
            MonthlyBudgetUsd: request.MonthlyBudgetUsd,
            UpdatedByUserId: adminUserId,
            CircuitBreakerFailureThreshold: request.CircuitBreakerFailureThreshold,
            CircuitBreakerOpenDurationSeconds: request.CircuitBreakerOpenDurationSeconds,
            CircuitBreakerSuccessThreshold: request.CircuitBreakerSuccessThreshold,
            FallbackChainJson: request.FallbackChainJson,
            DegradeThresholdPercent: request.DegradeThresholdPercent,
            BackgroundQuotaPercent: request.BackgroundQuotaPercent,
            EvaluationQuotaPercent: request.EvaluationQuotaPercent), ct).ConfigureAwait(false);

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
/// <remarks>
/// #4166: i tre campi della politica di budget sono opzionali; assenti, restano i valori salvati.
/// #4169: lo sono anche le soglie del circuit breaker e la catena di fallback, che il runtime non legge
/// e la pagina admin non manda più. Restano accettati finché le colonne non escono (contract).
/// </remarks>
internal record UpdateLlmSystemConfigRequest(
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    int? CircuitBreakerFailureThreshold = null,
    int? CircuitBreakerOpenDurationSeconds = null,
    int? CircuitBreakerSuccessThreshold = null,
    string? FallbackChainJson = null,
    int? DegradeThresholdPercent = null,
    int? BackgroundQuotaPercent = null,
    int? EvaluationQuotaPercent = null);
