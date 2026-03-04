using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

// Issue #3338: AI Token Usage Tracking per User
using GetUserDetailedAiUsageQuery = Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserDetailedAiUsageQuery;

namespace Api.Routing;

/// <summary>
/// Analytics dashboard and reporting endpoints (Admin only).
/// Handles AI request statistics, quality metrics, LLM health monitoring, and cost reporting.
/// </summary>
internal static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        MapRequestTrackingEndpoints(group);
        MapLlmHealthEndpoints(group);
        MapQualityEndpoints(group);
        MapDashboardEndpoints(group);
        MapLlmCostEndpoints(group);

        return group;
    }

    private static void MapRequestTrackingEndpoints(RouteGroupBuilder group)
    {
        // ADM-01: Admin dashboard endpoints
        // ADM-01: Admin dashboard endpoints
        group.MapGet("/admin/requests", HandleGetRequests);

        group.MapGet("/admin/stats", HandleGetStats);
    }

    private static void MapLlmHealthEndpoints(RouteGroupBuilder group)
    {
        // ISSUE-962 (BGAI-020): LLM provider health monitoring
        // ISSUE-962 (BGAI-020): LLM provider health monitoring
        group.MapGet("/admin/llm/health", HandleGetLlmHealth)
        .WithName("GetLlmHealth")
        .WithTags("Admin", "LLM")
        .WithSummary("Get LLM provider health status")
        .WithDescription("Returns real-time health monitoring for all LLM providers (circuit breaker, latency, success rate)")
        .Produces<LlmHealthStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static void MapQualityEndpoints(RouteGroupBuilder group)
    {
        // AI-11: Quality tracking endpoints
        // AI-11: Quality tracking endpoints
        group.MapGet("/admin/quality/low-responses", HandleGetLowResponses)
        .RequireAuthorization()
        .WithTags("Admin", "Quality")
        .Produces<LowQualityResponsesResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/admin/quality/report", HandleGetQualityReport)
        .RequireAuthorization()
        .WithTags("Admin", "Quality")
        .Produces<QualityReport>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapDashboardEndpoints(RouteGroupBuilder group)
    {
        // ADMIN-02: Analytics dashboard endpoints
        // ADMIN-02: Analytics dashboard endpoints
        group.MapGet("/admin/analytics", HandleGetAnalytics)
        .WithName("GetAnalytics")
        .WithTags("Admin")
        .WithDescription("Get analytics dashboard statistics with metrics and time-series trends")
        .Produces<DashboardStatsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Issue #877: Dashboard stats endpoint
        // Issue #877: Dashboard stats endpoint
        group.MapGet("/admin/dashboard/stats", HandleGetDashboardStats)
        .WithName("GetDashboardStats")
        .WithTags("Admin", "Dashboard")
        .WithDescription("Get comprehensive dashboard statistics for admin overview page. " +
                         "Returns aggregated metrics (users, sessions, games, PDFs), " +
                         "time-series trends (daily data points), and system health indicators. " +
                         "Supports filtering by date range, game, and user role.")
        .WithSummary("Admin Dashboard Overview Statistics")
        .Produces<DashboardStatsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi(operation =>
        {
            operation.Parameters[0].Description = "Start date for filtering (optional, defaults to 'days' ago from now)";
            operation.Parameters[1].Description = "End date for filtering (optional, defaults to now)";
            operation.Parameters[2].Description = "Number of days to look back (default: 30, used if fromDate/toDate not specified)";
            operation.Parameters[3].Description = "Filter by specific game ID (optional)";
            operation.Parameters[4].Description = "Filter by user role: 'admin', 'user', or 'editor' (optional)";
            return operation;
        });

        // Issue #4198: Lightweight overview stats for StatsOverview component
        group.MapGet("/admin/overview-stats", HandleGetOverviewStats)
        .WithName("GetOverviewStats")
        .WithTags("Admin", "Dashboard")
        .WithSummary("Admin Overview Statistics")
        .WithDescription("Lightweight overview stats: game counts, user counts, approval rate, pending approvals, recent submissions.")
        .Produces<Api.BoundedContexts.Administration.Application.DTOs.AdminOverviewStatsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Issue #874: Activity feed endpoint for admin dashboard
        // Issue #874: Activity feed endpoint for admin dashboard
        group.MapGet("/admin/activity", HandleGetRecentActivity)
        .WithName("GetRecentActivity")
        .WithTags("Admin")
        .WithDescription("Get recent activity feed for admin dashboard (last N system events)")
        .Produces<RecentActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/admin/analytics/export", HandleExportAnalytics)
        .WithName("ExportAnalytics")
        .WithTags("Admin")
        .WithDescription("Export analytics dashboard data in CSV or JSON format")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Issue #2790: Admin Dashboard Charts - API Requests & AI Usage
        group.MapGet("/admin/analytics/api-requests", HandleGetApiRequestsByDay)
        .WithName("GetApiRequestsByDay")
        .WithTags("Admin", "Charts")
        .WithDescription("Get API requests grouped by day for bar chart visualization")
        .Produces<ApiRequestByDayDto[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/admin/analytics/ai-usage", HandleGetAiUsageStats)
        .WithName("GetAiUsageStats")
        .WithTags("Admin", "Charts")
        .WithDescription("Get AI usage statistics by model for donut chart visualization")
        .Produces<AiUsageStatsDto[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapLlmCostEndpoints(RouteGroupBuilder group)
    {
        // ISSUE-960: BGAI-018 - LLM Cost Reporting
        // ISSUE-960: BGAI-018 - LLM Cost Reporting
        group.MapGet("/llm-costs/report", HandleGetLlmCostReport)
        .WithTags("Admin", "LLM", "Analytics")
        .WithName("GetLlmCostReport");

        group.MapGet("/llm-costs/daily", HandleGetDailyLlmCost)
        .WithTags("Admin", "LLM", "Analytics")
        .WithName("GetDailyLlmCost");

        group.MapPost("/llm-costs/check-alerts", HandleCheckLlmCostAlerts)
        .WithTags("Admin", "LLM", "Alerts")
        .WithName("CheckLlmCostAlerts");

        // Issue #3338: AI Token Usage Tracking per User - Admin can view any user's usage
        group.MapGet("/admin/users/{userId:guid}/ai-usage", HandleGetUserAiUsage)
        .WithName("GetUserAiUsage")
        .WithTags("Admin", "AI Usage")
        .WithSummary("Get detailed AI usage for a specific user")
        .WithDescription("Returns detailed AI usage statistics for the specified user including token counts, costs, and breakdowns by model and operation.")
        .Produces<Api.BoundedContexts.KnowledgeBase.Application.DTOs.UserAiUsageDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }


    private static async Task<IResult> HandleGetRequests(HttpContext context, IMediator mediator, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new Api.BoundedContexts.Administration.Application.Queries.GetAiRequestsQuery(
            limit,
            offset,
            endpoint,
            userId,
            gameId,
            startDate,
            endDate);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(new { requests = result.Requests, totalCount = result.TotalCount });
    }

    private static async Task<IResult> HandleGetStats(HttpContext context, IMediator mediator, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new Api.BoundedContexts.Administration.Application.Queries.GetAiRequestStatsQuery(startDate, endDate, userId, gameId);
        var stats = await mediator.Send(query, ct).ConfigureAwait(false);

        // ISSUE-1695: Feedback stats now available via CQRS
        var feedbackQuery = new GetFeedbackStatsQuery(startDate, endDate);
        var feedbackStats = await mediator.Send(feedbackQuery, ct).ConfigureAwait(false);

        return Results.Json(new
        {
            stats.TotalRequests,
            stats.AvgLatencyMs,
            stats.TotalTokens,
            stats.SuccessRate,
            stats.EndpointCounts,
            // Map to frontend schema (Issue #1695 followup)
            totalFeedback = feedbackStats.TotalFeedbacks,
            feedbackCounts = feedbackStats.FeedbackByOutcome
        });
    }

    private static async Task<IResult> HandleGetLlmHealth(HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var healthStatus = await mediator.Send(new GetLlmHealthQuery(), ct).ConfigureAwait(false);
        return Results.Json(healthStatus);
    }

    private static async Task<IResult> HandleGetLowResponses(
        HttpContext context,
        IMediator mediator,
        int limit = 100,
        int offset = 0,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        // Use CQRS Query to get low-quality responses
        var query = new GetLowQualityResponsesQuery(limit, offset, startDate, endDate);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetQualityReport(
        HttpContext context,
        IMediator mediator,
        int days = 7,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-days);

        var query = new Api.BoundedContexts.Administration.Application.Queries.QualityReports.GenerateQualityReportQuery
        {
            StartDate = startDate,
            EndDate = endDate,
            Days = days
        };
        var report = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(report);
    }

    private static async Task<IResult> HandleGetAnalytics(
        HttpContext context,
        IMediator mediator,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int days = 30,
        string? gameId = null,
        string? roleFilter = null,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAdminStatsQuery(fromDate, toDate, days, gameId, roleFilter);
        var stats = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(stats);
    }

    private static async Task<IResult> HandleGetDashboardStats(
        HttpContext context,
        IMediator mediator,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int days = 30,
        string? gameId = null,
        string? roleFilter = null,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAdminStatsQuery(fromDate, toDate, days, gameId, roleFilter);
        var stats = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(stats);
    }

    /// <summary>
    /// Issue #4198: Lightweight overview stats for StatsOverview component.
    /// </summary>
    private static async Task<IResult> HandleGetOverviewStats(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new Api.BoundedContexts.Administration.Application.Queries.GetAdminOverviewStatsQuery();
        var stats = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(stats);
    }

    private static async Task<IResult> HandleGetRecentActivity(
        HttpContext context,
        IMediator mediator,
        int limit = 10,
        DateTime? since = null,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetRecentActivityQuery(limit, since);
        var activity = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(activity);
    }

    private static async Task<IResult> HandleExportAnalytics(
        HttpContext context,
        IMediator mediator,
        ExportDataRequest request,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ExportStatsCommand(request.Format, request.FromDate, request.ToDate, request.GameId);
        var data = await mediator.Send(command, ct).ConfigureAwait(false);
        var contentType = request.Format.ToLowerInvariant() switch
        {
            "csv" => "text/csv",
            "json" => "application/json",
            _ => "text/plain"
        };
        var filename = $"analytics-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.{request.Format.ToLowerInvariant()}";

        return Results.File(
            System.Text.Encoding.UTF8.GetBytes(data),
            contentType,
            filename);
    }

    private static async Task<IResult> HandleGetLlmCostReport(
        HttpContext context,
        IMediator mediator,
        string? startDate,
        string? endDate,
        Guid? userId,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        // Default to last 30 days if not specified
        DateOnly start;
        if (string.IsNullOrWhiteSpace(startDate))
            start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        else if (!DateOnly.TryParse(startDate, CultureInfo.InvariantCulture, out start))
            return Results.BadRequest(new { error = $"Invalid startDate format: {startDate}" });

        DateOnly end;
        if (string.IsNullOrWhiteSpace(endDate))
            end = DateOnly.FromDateTime(DateTime.UtcNow);
        else if (!DateOnly.TryParse(endDate, CultureInfo.InvariantCulture, out end))
            return Results.BadRequest(new { error = $"Invalid endDate format: {endDate}" });

        var query = new GetLlmCostReportQuery
        {
            StartDate = start,
            EndDate = end,
            UserId = userId
        };

        var report = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(report);
    }

    private static async Task<IResult> HandleGetDailyLlmCost(
        HttpContext context,
        IMediator mediator,
        string? date,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var targetDate = string.IsNullOrWhiteSpace(date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.Parse(date, CultureInfo.InvariantCulture);

        var query = new GetLlmCostReportQuery
        {
            StartDate = targetDate,
            EndDate = targetDate
        };

        var report = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(new
        {
            date = targetDate,
            totalCost = report.DailyCost,
            exceedsThreshold = report.ExceedsThreshold,
            threshold = report.ThresholdAmount,
            costsByProvider = report.CostsByProvider,
            costsByRole = report.CostsByRole
        });
    }

    private static async Task<IResult> HandleCheckLlmCostAlerts(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        // Use CQRS Command to check all thresholds
        var result = await mediator.Send(new CheckLlmCostAlertsCommand(), ct).ConfigureAwait(false);

        return Results.Json(new { success = result.Success, message = result.Message });
    }

    // Issue #2790: Admin Dashboard Charts - Handler methods
    private static async Task<IResult> HandleGetApiRequestsByDay(
        HttpContext context,
        IMediator mediator,
        int days = 7,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetApiRequestsByDayQuery(days);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAiUsageStats(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAiUsageStatsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #3338: AI Token Usage Tracking per User - Handler method
    private static async Task<IResult> HandleGetUserAiUsage(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        int days = 30,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-days);

        var query = new GetUserDetailedAiUsageQuery(userId, startDate, endDate);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }
}
