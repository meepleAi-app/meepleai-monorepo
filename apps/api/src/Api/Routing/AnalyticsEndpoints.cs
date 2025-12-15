using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Api.Routing;

/// <summary>
/// Analytics dashboard and reporting endpoints (Admin only).
/// Handles AI request statistics, quality metrics, LLM health monitoring, and cost reporting.
/// </summary>
internal static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        // ADM-01: Admin dashboard endpoints
        group.MapGet("/admin/requests", async (HttpContext context, IMediator mediator, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) =>
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
        });

        group.MapGet("/admin/stats", async (HttpContext context, IMediator mediator, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default) =>
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
        });

        // ISSUE-962 (BGAI-020): LLM provider health monitoring
        group.MapGet("/admin/llm/health", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var healthStatus = await mediator.Send(new GetLlmHealthQuery(), ct).ConfigureAwait(false);
            return Results.Json(healthStatus);
        })
        .WithName("GetLlmHealth")
        .WithTags("Admin", "LLM")
        .WithSummary("Get LLM provider health status")
        .WithDescription("Returns real-time health monitoring for all LLM providers (circuit breaker, latency, success rate)")
        .Produces<LlmHealthStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // AI-11: Quality tracking endpoints
        group.MapGet("/admin/quality/low-responses", async (
            HttpContext context,
            IMediator mediator,
            int limit = 100,
            int offset = 0,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Use CQRS Query to get low-quality responses
            var query = new GetLowQualityResponsesQuery(limit, offset, startDate, endDate);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Admin", "Quality")
        .Produces<LowQualityResponsesResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/admin/quality/report", async (
            HttpContext context,
            IMediator mediator,
            int days = 7,
            CancellationToken ct = default) =>
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
        })
        .RequireAuthorization()
        .WithTags("Admin", "Quality")
        .Produces<QualityReport>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // ADMIN-02: Analytics dashboard endpoints
        group.MapGet("/admin/analytics", async (
            HttpContext context,
            IMediator mediator,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int days = 30,
            string? gameId = null,
            string? roleFilter = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAdminStatsQuery(fromDate, toDate, days, gameId, roleFilter);
            var stats = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(stats);
        })
        .WithName("GetAnalytics")
        .WithTags("Admin")
        .WithDescription("Get analytics dashboard statistics with metrics and time-series trends")
        .Produces<DashboardStatsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Issue #877: Dashboard stats endpoint
        group.MapGet("/admin/dashboard/stats", async (
            HttpContext context,
            IMediator mediator,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int days = 30,
            string? gameId = null,
            string? roleFilter = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAdminStatsQuery(fromDate, toDate, days, gameId, roleFilter);
            var stats = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(stats);
        })
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

        // Issue #874: Activity feed endpoint for admin dashboard
        group.MapGet("/admin/activity", async (
            HttpContext context,
            IMediator mediator,
            int limit = 10,
            DateTime? since = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetRecentActivityQuery(limit, since);
            var activity = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(activity);
        })
        .WithName("GetRecentActivity")
        .WithTags("Admin")
        .WithDescription("Get recent activity feed for admin dashboard (last N system events)")
        .Produces<RecentActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/admin/analytics/export", async (
            HttpContext context,
            IMediator mediator,
            ExportDataRequest request,
            CancellationToken ct = default) =>
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
        })
        .WithName("ExportAnalytics")
        .WithTags("Admin")
        .WithDescription("Export analytics dashboard data in CSV or JSON format")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // ISSUE-960: BGAI-018 - LLM Cost Reporting
        group.MapGet("/llm-costs/report", async (
            HttpContext context,
            IMediator mediator,
            string? startDate,
            string? endDate,
            Guid? userId,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Default to last 30 days if not specified
            var start = string.IsNullOrWhiteSpace(startDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
                : DateOnly.Parse(startDate, CultureInfo.InvariantCulture);

            var end = string.IsNullOrWhiteSpace(endDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(endDate, CultureInfo.InvariantCulture);

            var query = new GetLlmCostReportQuery
            {
                StartDate = start,
                EndDate = end,
                UserId = userId
            };

            var report = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(report);
        })
        .WithTags("Admin", "LLM", "Analytics")
        .WithName("GetLlmCostReport");

        group.MapGet("/llm-costs/daily", async (
            HttpContext context,
            IMediator mediator,
            string? date,
            CancellationToken ct) =>
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
        })
        .WithTags("Admin", "LLM", "Analytics")
        .WithName("GetDailyLlmCost");

        group.MapPost("/llm-costs/check-alerts", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Use CQRS Command to check all thresholds
            var result = await mediator.Send(new CheckLlmCostAlertsCommand(), ct).ConfigureAwait(false);

            return Results.Json(new { success = result.Success, message = result.Message });
        })
        .WithTags("Admin", "LLM", "Alerts")
        .WithName("CheckLlmCostAlerts");

        return group;
    }
}
