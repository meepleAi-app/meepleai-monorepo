using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Agent Metrics Dashboard.
/// Issue #3382: Agent Metrics Dashboard - Usage, Costs, Accuracy.
/// </summary>
internal static class AdminAgentMetricsEndpoints
{
    public static void MapAdminAgentMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/agents/metrics")
            .WithTags("Admin - Agent Metrics")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/agents/metrics
        // Get aggregated agent metrics with optional filters
        group.MapGet("/", GetAgentMetrics)
            .WithName("GetAgentMetrics")
            .WithSummary("Get aggregated agent metrics")
            .WithDescription(@"
Returns aggregated metrics for all agents including:
- Total invocations, tokens, and cost
- Average latency and confidence scores
- Top queries by frequency
- Cost breakdown by model
- Usage over time series

**Filters:**
- startDate/endDate: Date range (default: last 30 days)
- typologyId: Filter by specific typology
- strategy: Filter by strategy name
");

        // GET /api/v1/admin/agents/metrics/{id}
        // Get metrics for a specific agent/typology
        group.MapGet("/{id:guid}", GetSingleAgentMetrics)
            .WithName("GetSingleAgentMetrics")
            .WithSummary("Get metrics for a specific agent/typology")
            .WithDescription(@"
Returns detailed metrics for a single agent typology including:
- Total invocations, tokens, and cost
- Average latency and confidence
- Last invoked timestamp
- Usage over time series
- Model breakdown
");

        // GET /api/v1/admin/agents/metrics/top
        // Get top agents by usage or cost
        group.MapGet("/top", GetTopAgents)
            .WithName("GetTopAgents")
            .WithSummary("Get top agents by usage, cost, or confidence")
            .WithDescription(@"
Returns top agents sorted by the specified criteria.

**Sort Options:**
- invocations (default): Most used agents
- cost: Highest cost agents
- confidence: Highest confidence agents
");

        // GET /api/v1/admin/agents/metrics/arbitro/beta
        // Get Arbitro beta testing metrics
        group.MapGet("/arbitro/beta", GetArbitroBetaMetrics)
            .WithName("GetArbitroBetaMetrics")
            .WithSummary("Get Arbitro Agent beta testing metrics")
            .WithDescription(@"
Returns comprehensive beta testing metrics for Arbitro Agent including:
- Overall accuracy percentage (target: >90%)
- Conflict resolution accuracy (target: >85%)
- Average user satisfaction rating (target: >4.0/5.0)
- Decision distribution (VALID/INVALID/UNCERTAIN breakdown)
- FAQ fast-path hit rate and top FAQ entries
- Accuracy trend over time (daily aggregates)

**Filters:**
- gameSessionId: Filter by specific game session
- fromDate/toDate: Date range filter

**Beta Testing Targets:**
- Validation accuracy: >90%
- Conflict resolution: >85%
- User satisfaction: >4.0/5.0
- Performance: <100ms P95 rule retrieval, <2s P95 total
");
    }

    private static async Task<IResult> GetAgentMetrics(
        [FromServices] IMediator mediator,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] Guid? typologyId,
        [FromQuery] string? strategy,
        CancellationToken ct)
    {
        var query = new GetAgentMetricsQuery(
            StartDate: startDate,
            EndDate: endDate,
            TypologyId: typologyId,
            Strategy: strategy);

        var metrics = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(metrics);
    }

    private static async Task<IResult> GetSingleAgentMetrics(
        [FromRoute] Guid id,
        [FromServices] IMediator mediator,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var query = new GetSingleAgentMetricsQuery(
            TypologyId: id,
            StartDate: startDate,
            EndDate: endDate);

        var metrics = await mediator.Send(query, ct).ConfigureAwait(false);

        if (metrics == null)
        {
            return Results.NotFound(new { error = "Agent typology not found" });
        }

        return Results.Ok(metrics);
    }

    private static async Task<IResult> GetTopAgents(
        [FromServices] IMediator mediator,
        [FromQuery] int limit = 10,
        [FromQuery] string sortBy = "invocations",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken ct = default)
    {
        // Validate limit
        if (limit < 1 || limit > 100)
        {
            return Results.BadRequest(new { error = "Limit must be between 1 and 100" });
        }

        var query = new GetTopAgentsQuery(
            Limit: limit,
            SortBy: sortBy,
            StartDate: startDate,
            EndDate: endDate);

        var topAgents = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(topAgents);
    }

    private static async Task<IResult> GetArbitroBetaMetrics(
        [FromServices] IMediator mediator,
        [FromQuery] Guid? gameSessionId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var query = new GetArbitroBetaMetricsQuery
        {
            GameSessionId = gameSessionId,
            FromDate = fromDate,
            ToDate = toDate
        };

        var metrics = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(metrics);
    }
}
