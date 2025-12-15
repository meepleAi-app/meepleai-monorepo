using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// ISSUE-1725: Admin endpoints for LLM analytics and cost optimization.
/// Provides efficiency reports, model recommendations, and optimization insights.
/// </summary>
internal static class LlmAnalyticsEndpoints
{
    public static void MapLlmAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/llm")
            .WithTags("Admin - LLM Analytics")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/llm/efficiency-report?startDate=2025-12-01&endDate=2025-12-31
        group.MapGet("/efficiency-report", GetQueryEfficiencyReport)
            .WithName("GetQueryEfficiencyReport")
            .WithSummary("Get LLM query efficiency analysis report")
            .WithDescription("Analyzes token usage patterns and provides optimization recommendations for a date range");

        // GET /api/v1/admin/llm/monthly-report?year=2025&month=12
        group.MapGet("/monthly-report", GetMonthlyOptimizationReport)
            .WithName("GetMonthlyOptimizationReport")
            .WithSummary("Get comprehensive monthly LLM optimization report")
            .WithDescription("Combines efficiency analysis, cache correlation, and model recommendations");

        // GET /api/v1/admin/llm/model-recommendations?useCase=qa&prioritizeCost=false
        group.MapGet("/model-recommendations", GetModelRecommendations)
            .WithName("GetModelRecommendations")
            .WithSummary("Get LLM model recommendations for use cases")
            .WithDescription("Provides cost/quality trade-off analysis and model selection guidance");
    }

    private static async Task<IResult> GetQueryEfficiencyReport(
        [FromServices] IMediator mediator,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        // Default to last 30 days if not specified
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-30);

        var query = new GetQueryEfficiencyReportQuery
        {
            StartDate = start,
            EndDate = end
        };

        var report = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetMonthlyOptimizationReport(
        [FromServices] IMediator mediator,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        // Default to current month if not specified
        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;

        if (targetMonth < 1 || targetMonth > 12)
        {
            return Results.BadRequest(new { error = "Month must be between 1 and 12" });
        }

        var query = new GetMonthlyOptimizationReportQuery
        {
            Year = targetYear,
            Month = targetMonth
        };

        var report = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetModelRecommendations(
        [FromServices] Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics.IModelRecommendationService recommendationService,
        [FromQuery] string? useCase,
        [FromQuery] bool prioritizeCost = false,
        CancellationToken ct = default)
    {
        var targetUseCase = useCase ?? "qa";

        // Get recommendation
        var recommendation = await recommendationService.GetRecommendationAsync(
            targetUseCase,
            prioritizeCost,
            ct).ConfigureAwait(false);

        // Get model comparisons
        var comparisons = await recommendationService.CompareModelsAsync(ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            recommendation,
            comparisons,
            useCase = targetUseCase,
            prioritizeCost
        });
    }
}
