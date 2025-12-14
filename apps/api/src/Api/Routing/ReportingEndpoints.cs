using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// HTTP endpoints for report generation and scheduling
/// ISSUE-916: Admin reporting endpoints
/// </summary>
public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/reports")
            .RequireAuthorization()
            .RequireAuthorization(policy => policy.RequireRole("Admin")) // ISSUE-916: Admin-only access
            .WithTags("Admin - Reporting");

        // Generate report on-demand
        group.MapPost("/generate", async (
            [FromBody] GenerateReportRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("POST /api/v1/admin/reports/generate - Template={Template}, Format={Format}",
                request.Template, request.Format);

            var command = new GenerateReportCommand
            {
                Template = request.Template,
                Format = request.Format,
                Parameters = request.Parameters ?? new Dictionary<string, object>(StringComparer.Ordinal)
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            // Return file download
            return Results.File(
                result.Content,
                request.Format.ToContentType(),
                result.FileName);
        })
        .WithName("GenerateReport")
        .WithSummary("Generate a report on-demand")
        .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // Schedule recurring report
        group.MapPost("/schedule", async (
            [FromBody] ScheduleReportRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var userId = context.User.FindFirst("user_id")?.Value
                ?? throw new UnauthorizedAccessException("User ID not found");

            logger.LogInformation("POST /api/v1/admin/reports/schedule - Name={Name}, Template={Template}",
                request.Name, request.Template);

            var command = new ScheduleReportCommand
            {
                Name = request.Name,
                Description = request.Description,
                Template = request.Template,
                Format = request.Format,
                Parameters = request.Parameters ?? new Dictionary<string, object>(StringComparer.Ordinal),
                ScheduleExpression = request.ScheduleExpression,
                CreatedBy = userId,
                EmailRecipients = request.EmailRecipients // ISSUE-918
            };

            var reportId = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(new { reportId, message = "Report scheduled successfully" });
        })
        .WithName("ScheduleReport")
        .WithSummary("Schedule a recurring report")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // Get all scheduled reports
        group.MapGet("/scheduled", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogDebug("GET /api/v1/admin/reports/scheduled");

            var query = new GetScheduledReportsQuery();
            var reports = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(reports);
        })
        .WithName("GetScheduledReports")
        .WithSummary("Get all scheduled reports")
        .Produces<IReadOnlyList<ScheduledReportDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Get report executions
        group.MapGet("/executions", async (
            Guid? reportId,
            int limit,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogDebug("GET /api/v1/admin/reports/executions - ReportId={ReportId}, Limit={Limit}",
                reportId, limit);

            var query = new GetReportExecutionsQuery
            {
                ReportId = reportId,
                Limit = limit
            };

            var executions = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(executions);
        })
        .WithName("GetReportExecutions")
        .WithSummary("Get report execution history")
        .Produces<IReadOnlyList<ReportExecutionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Update report schedule
        group.MapPut("/{reportId:guid}/schedule", async (
            Guid reportId,
            [FromBody] UpdateScheduleRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("PUT /api/v1/admin/reports/{ReportId}/schedule", reportId);

            var command = new UpdateReportScheduleCommand
            {
                ReportId = reportId,
                ScheduleExpression = request.ScheduleExpression,
                IsActive = request.IsActive
            };

            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            return success
                ? Results.Ok(new { message = "Schedule updated successfully" })
                : Results.NotFound(new { message = "Report not found" });
        })
        .WithName("UpdateReportSchedule")
        .WithSummary("Update or cancel a report schedule")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

// Request DTOs
public sealed record GenerateReportRequest(
    ReportTemplate Template,
    ReportFormat Format,
    IReadOnlyDictionary<string, object>? Parameters);

public sealed record ScheduleReportRequest(
    string Name,
    string Description,
    ReportTemplate Template,
    ReportFormat Format,
    IReadOnlyDictionary<string, object>? Parameters,
    string ScheduleExpression,
    IReadOnlyList<string>? EmailRecipients); // ISSUE-918: Email delivery integration

public sealed record UpdateScheduleRequest(
    string? ScheduleExpression,
    bool IsActive);
