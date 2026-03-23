using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Issue #3304: RAG Dashboard API endpoints.
/// Provides configuration and metrics endpoints for the RAG Dashboard.
/// </summary>
internal static class RagDashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonCamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static RouteGroupBuilder MapRagDashboardEndpoints(this RouteGroupBuilder group)
    {
        var ragGroup = group.MapGroup("/rag-dashboard")
            .WithTags("RAG Dashboard");

        // ========================================
        // Configuration Endpoints
        // ========================================

        ragGroup.MapGet("/options", HandleGetOptions)
            .WithName("GetRagDashboardOptions")
            .WithSummary("Get available RAG dashboard options")
            .WithDescription("Returns all available LLM models, reranker models, and retrieval strategies.")
            .Produces<RagDashboardOptionsDto>(StatusCodes.Status200OK)
            .AllowAnonymous();

        ragGroup.MapGet("/config", HandleGetConfig)
            .WithName("GetRagConfig")
            .WithSummary("Get RAG configuration")
            .WithDescription("Returns the current RAG configuration for the authenticated user or defaults.")
            .Produces<RagConfigDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        ragGroup.MapPut("/config", HandleSaveConfig)
            .WithName("SaveRagConfig")
            .WithSummary("Save RAG configuration")
            .WithDescription("Saves the RAG configuration for the authenticated user.")
            .Produces<RagConfigDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        ragGroup.MapPost("/config/reset", HandleResetConfig)
            .WithName("ResetRagConfig")
            .WithSummary("Reset RAG configuration to defaults")
            .WithDescription("Resets the RAG configuration to default values.")
            .Produces<RagConfigDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // ========================================
        // Metrics Endpoints
        // ========================================

        ragGroup.MapGet("/overview", HandleGetOverview)
            .WithName("GetRagDashboardOverview")
            .WithSummary("Get RAG dashboard overview")
            .WithDescription("Returns comprehensive metrics for all RAG strategies including aggregated stats.")
            .Produces<RagDashboardOverviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        ragGroup.MapGet("/strategies/{strategyId}/metrics", HandleGetStrategyMetrics)
            .WithName("GetStrategyMetrics")
            .WithSummary("Get metrics for a specific strategy")
            .WithDescription("Returns detailed metrics for a single RAG strategy.")
            .Produces<StrategyMetricsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        ragGroup.MapGet("/strategies/{strategyId}/time-series", HandleGetStrategyTimeSeries)
            .WithName("GetStrategyTimeSeries")
            .WithSummary("Get time series metrics for a strategy")
            .WithDescription("Returns time series data for latency, relevance, query count, and cost trends.")
            .Produces<StrategyTimeSeriesMetricsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        ragGroup.MapGet("/strategies/compare", HandleGetStrategyComparison)
            .WithName("GetStrategyComparison")
            .WithSummary("Compare multiple strategies")
            .WithDescription("Returns a comparison of multiple RAG strategies with rankings and recommendations.")
            .Produces<StrategyComparisonDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // ========================================
        // Admin Live Testing Endpoint
        // ========================================

        MapAdminQueryStreamEndpoint(ragGroup);

        return group;
    }

    // ========================================
    // Handler Methods
    // ========================================

    private static async Task<IResult> HandleGetOptions(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetRagDashboardOptionsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetConfig(
        HttpContext context,
        IMediator mediator,
        [FromQuery] string? strategy,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        var query = new GetRagConfigQuery
        {
            UserId = session!.User!.Id,
            Strategy = strategy
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSaveConfig(
        HttpContext context,
        IMediator mediator,
        [FromBody] RagConfigDto config,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "User {UserId} saving RAG config for strategy {Strategy}",
            session!.User!.Id,
            config.ActiveStrategy);

        var command = new SaveRagConfigCommand
        {
            UserId = session.User.Id,
            Config = config
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleResetConfig(
        HttpContext context,
        IMediator mediator,
        [FromQuery] string? strategy,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "User {UserId} resetting RAG config for strategy {Strategy}",
            session!.User!.Id,
            strategy ?? "all");

        var command = new ResetRagConfigCommand
        {
            UserId = session.User.Id,
            Strategy = strategy
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetOverview(
        HttpContext context,
        IMediator mediator,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        var query = new GetRagDashboardOverviewQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetStrategyMetrics(
        HttpContext context,
        IMediator mediator,
        string strategyId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        var validStrategies = new[] { "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic" };
        if (!validStrategies.Contains(strategyId, StringComparer.OrdinalIgnoreCase))
        {
            return Results.NotFound(new { error = $"Strategy '{strategyId}' not found" });
        }

        var query = new GetStrategyMetricsQuery
        {
            StrategyId = strategyId,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetStrategyTimeSeries(
        HttpContext context,
        IMediator mediator,
        string strategyId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        var validStrategies = new[] { "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic" };
        if (!validStrategies.Contains(strategyId, StringComparer.OrdinalIgnoreCase))
        {
            return Results.NotFound(new { error = $"Strategy '{strategyId}' not found" });
        }

        var validGranularities = new[] { "hour", "day", "week" };
        if (granularity != null && !validGranularities.Contains(granularity, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Granularity must be 'hour', 'day', or 'week'" });
        }

        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = strategyId,
            StartDate = startDate,
            EndDate = endDate,
            Granularity = granularity ?? "hour"
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // ========================================
    // Admin Live Testing
    // ========================================

    private static void MapAdminQueryStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/query/stream", async (
            [FromBody] AdminLiveQueryRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(request.Query))
                return Results.BadRequest(new { error = "Query is required" });

            if (string.IsNullOrWhiteSpace(request.GameContext))
                return Results.BadRequest(new { error = "GameContext is required" });

            var searchResults = await mediator.Send(
                new SearchGamesQuery(request.GameContext, session!.User!.Id, 5), ct)
                .ConfigureAwait(false);
            var gameId = searchResults?.FirstOrDefault()?.Id;

            if (gameId == null)
            {
                return Results.BadRequest(new
                {
                    error = $"Game '{request.GameContext}' not found in catalog"
                });
            }

            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Accel-Buffering", "no");

            logger.LogInformation(
                "Admin {AdminId} running live RAG test for game {GameId}: '{Query}'",
                session!.User!.Id, gameId, LogValueSanitizer.Sanitize(request.Query));

            var streamQuery = new StreamQaQuery(GameId: gameId.Value.ToString(), Query: request.Query);

            try
            {
                await foreach (var evt in mediator.CreateStream(streamQuery, ct).ConfigureAwait(false))
                {
                    var json = JsonSerializer.Serialize(evt, JsonCamelCaseOptions);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation(oce, "Admin live RAG test stream cancelled for game {GameId}", gameId);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in admin live RAG test stream for game {GameId}", gameId);
            }
#pragma warning restore CA1031

            return Results.Empty;
        })
        .WithName("AdminRagLiveQueryStream")
        .WithSummary("Live test RAG query with SSE streaming (Admin)")
        .WithDescription("Streams RAG Q&A results in real-time using Server-Sent Events. Requires admin session.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleGetStrategyComparison(
        HttpContext context,
        IMediator mediator,
        [FromQuery] string? strategyIds,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.TryGetActiveSession();
        if (!authorized) return error!;

        IReadOnlyList<string>? strategies = null;
        if (!string.IsNullOrWhiteSpace(strategyIds))
        {
            strategies = strategyIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var query = new GetStrategyComparisonQuery
        {
            StrategyIds = strategies,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for admin live RAG query streaming endpoint.
/// </summary>
internal sealed record AdminLiveQueryRequest(string Query, string GameContext);
