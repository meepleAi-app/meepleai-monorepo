using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace Api.Routing;

/// <summary>
/// Admin and system management endpoints.
/// Handles logs, users, admin operations, prompts, API keys, configurations, sessions, analytics, and N8N.
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
group.MapGet("/logs", async (HttpContext context, AiRequestLogService logService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await logService.GetRequestsAsync(limit: 100, ct: ct);

    var response = result.Requests
        .Select(log =>
        {
            var level = string.Equals(log.Status, "Error", StringComparison.OrdinalIgnoreCase)
                ? "ERROR"
                : "INFO";

            var message = !string.IsNullOrWhiteSpace(log.ResponseSnippet)
                ? log.ResponseSnippet!
                : !string.IsNullOrWhiteSpace(log.Query)
                    ? log.Query!
                    : $"{log.Endpoint} request ({log.Status})";

            return new LogEntryResponse(
                log.CreatedAt,
                level,
                message,
                log.Id,
                log.UserId,
                log.GameId
            );
        })
        .ToList();

    return Results.Json(response);
});

// API-01: Authentication endpoints (versioned)

group.MapGet("/users/search", async (
    string query,
    MeepleAiDbContext db,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
    {
        return Results.Ok(Array.Empty<UserSearchResultDto>());
    }

    logger.LogInformation("User {UserId} searching for users with query: {Query}", userId, query);

    var users = await db.Users
        .Where(u => (u.DisplayName != null && u.DisplayName.Contains(query)) || u.Email.Contains(query))
        .OrderBy(u => u.DisplayName ?? u.Email)
        .Take(10)
        .AsNoTracking()
        .Select(u => new UserSearchResultDto(u.Id, u.DisplayName ?? u.Email, u.Email))
        .ToListAsync(ct);

    logger.LogInformation("Found {Count} users matching query: {Query}", users.Count, query);
    return Results.Ok(users);
})
.RequireAuthorization()
.WithName("SearchUsers")
.WithTags("Users")
.WithDescription("Search users by display name or email for @mention autocomplete (max 10 results)")
.Produces<IEnumerable<UserSearchResultDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

// EDIT-07: Bulk RuleSpec operations

group.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, RuleSpecService rules, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var spec = await rules.GetOrCreateDemoAsync(request.gameId, ct);
    return Results.Json(new { ok = true, spec });
});

// ADM-01: Admin dashboard endpoints
group.MapGet("/admin/requests", async (HttpContext context, AiRequestLogService logService, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await logService.GetRequestsAsync(
        limit,
        offset,
        endpoint,
        userId,
        gameId,
        startDate,
        endDate,
        ct);

    return Results.Json(new { requests = result.Requests, totalCount = result.TotalCount });
});

group.MapGet("/admin/stats", async (HttpContext context, AiRequestLogService logService, AgentFeedbackService feedbackService, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var stats = await logService.GetStatsAsync(startDate, endDate, userId, gameId, ct);
    var feedbackStats = await feedbackService.GetStatsAsync(null, userId, gameId, startDate, endDate, ct);

    return Results.Json(new
    {
        stats.TotalRequests,
        stats.AvgLatencyMs,
        stats.TotalTokens,
        stats.SuccessRate,
        stats.EndpointCounts,
        feedbackCounts = feedbackStats.OutcomeCounts,
        totalFeedback = feedbackStats.TotalFeedback,
        feedbackByEndpoint = feedbackStats.EndpointOutcomeCounts
    });
});

// AI-11: Quality tracking endpoints
group.MapGet("/admin/quality/low-responses", async (
    HttpContext context,
    MeepleAiDbContext dbContext,
    int limit = 100,
    int offset = 0,
    DateTime? startDate = null,
    DateTime? endDate = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Convert DateTime parameters to UTC if they have Kind=Unspecified (from query string parsing)
    // PostgreSQL requires UTC DateTimes
    if (startDate.HasValue && startDate.Value.Kind == DateTimeKind.Unspecified)
        startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
    if (endDate.HasValue && endDate.Value.Kind == DateTimeKind.Unspecified)
        endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);

    // Query low-quality responses with filters
    var query = dbContext.AiRequestLogs.Where(log => log.IsLowQuality);

    if (startDate.HasValue)
        query = query.Where(log => log.CreatedAt >= startDate.Value);
    if (endDate.HasValue)
        query = query.Where(log => log.CreatedAt <= endDate.Value);

    var totalCount = await query.CountAsync(ct);
    var responses = await query
        .OrderByDescending(log => log.CreatedAt)
        .Skip(offset)
        .Take(limit)
        .Select(log => new LowQualityResponseDto(
            Guid.Parse(log.Id),
            log.CreatedAt,
            log.Query ?? string.Empty,
            log.RagConfidence ?? 0.0,
            log.LlmConfidence ?? 0.0,
            log.CitationQuality ?? 0.0,
            log.OverallConfidence ?? 0.0,
            log.IsLowQuality
        ))
        .ToListAsync(ct);

    return Results.Ok(new LowQualityResponsesResult(totalCount, responses));
})
.RequireAuthorization()
.WithTags("Admin", "Quality")
.Produces<LowQualityResponsesResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

group.MapGet("/admin/quality/report", async (
    HttpContext context,
    IQualityReportService reportService,
    int days = 7) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var endDate = DateTime.UtcNow;
    var startDate = endDate.AddDays(-days);
    var report = await reportService.GenerateReportAsync(startDate, endDate);
    return Results.Ok(report);
})
.RequireAuthorization()
.WithTags("Admin", "Quality")
.Produces<QualityReport>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ADM-02: n8n workflow configuration endpoints
group.MapGet("/admin/n8n", async (HttpContext context, N8nConfigService n8nService, IFeatureFlagService featureFlags, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // CONFIG-05: Check if n8n integration feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.N8nIntegration"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "n8n integration is currently disabled", featureName = "Features.N8nIntegration" },
            statusCode: 403);
    }

    var configs = await n8nService.GetConfigsAsync(ct);
    return Results.Json(new { configs });
});

group.MapGet("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await n8nService.GetConfigAsync(configId, ct);

    if (config == null)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    return Results.Json(config);
});

group.MapPost("/admin/n8n", async (CreateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.Id, request.Name);
        var config = await n8nService.CreateConfigAsync(session.User.Id, request, ct);
        logger.LogInformation("n8n config {ConfigId} created successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

group.MapPut("/admin/n8n/{configId}", async (string configId, UpdateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session.User.Id, configId);
        var config = await n8nService.UpdateConfigAsync(configId, request, ct);
        logger.LogInformation("n8n config {ConfigId} updated successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

group.MapDelete("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session.User.Id, configId);
    var deleted = await n8nService.DeleteConfigAsync(configId, ct);

    if (!deleted)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("n8n config {ConfigId} deleted successfully", configId);
    return Results.Json(new { ok = true });
});

group.MapPost("/admin/n8n/{configId}/test", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session.User.Id, configId);
        var result = await n8nService.TestConnectionAsync(configId, ct);
        logger.LogInformation("n8n config {ConfigId} test result: {Success}", configId, result.Success);
        return Results.Json(result);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to test n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// N8N-04: Workflow template endpoints
group.MapGet("/n8n/templates", async (
    string? category,
    N8nTemplateService templateService,
    CancellationToken ct) =>
{
    var templates = await templateService.GetTemplatesAsync(category, ct);
    return Results.Ok(templates);
})
.RequireAuthorization()
.WithName("GetN8nTemplates")
.WithTags("N8N")
.WithDescription("Get all n8n workflow templates, optionally filtered by category");

group.MapGet("/n8n/templates/{id}", async (
    string id,
    N8nTemplateService templateService,
    CancellationToken ct) =>
{
    var template = await templateService.GetTemplateAsync(id, ct);
    if (template == null)
    {
        return Results.NotFound(new { error = $"Template '{id}' not found" });
    }

    return Results.Ok(template);
})
.RequireAuthorization()
.WithName("GetN8nTemplate")
.WithTags("N8N")
.WithDescription("Get a specific n8n workflow template by ID with full details");

group.MapPost("/n8n/templates/{id}/import", async (
    string id,
    ImportTemplateRequest request,
    N8nTemplateService templateService,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Extract userId from authenticated session
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Problem("Session not found", statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        logger.LogInformation("User {UserId} importing n8n template {TemplateId}", session.User.Id, id);
        var result = await templateService.ImportTemplateAsync(id, request.Parameters, session.User.Id, ct);
        logger.LogInformation("Template {TemplateId} imported successfully as workflow {WorkflowId}", id, result.WorkflowId);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to import template {TemplateId}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (N8nTemplateService)
        logger.LogError(ex, "Unexpected error importing template {TemplateId}", id);
        return Results.Problem("An unexpected error occurred while importing the template");
    }
})
.RequireAuthorization()
.WithName("ImportN8nTemplate")
.WithTags("N8N")
.WithDescription("Import an n8n workflow template with parameter substitution");

group.MapPost("/n8n/templates/validate", async (
    ValidateTemplateRequest request,
    N8nTemplateService templateService,
    HttpContext context,
    CancellationToken ct) =>
{
    // Extract session for role check (RequireAuthorization ensures authentication)
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Problem("Session not found", statusCode: StatusCodes.Status500InternalServerError);
    }

    // Only admins can validate templates
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    var result = templateService.ValidateTemplate(request.TemplateJson);
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("ValidateN8nTemplate")
.WithTags("N8N")
.WithDescription("Validate n8n workflow template JSON structure (admin only)");

// AUTH-03: Session management endpoints
group.MapGet("/admin/sessions", async (HttpContext context, ISessionManagementService sessionManagement, int limit = 100, string? userId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var sessions = await sessionManagement.GetAllSessionsAsync(userId, limit, ct);
    return Results.Json(sessions);
});

group.MapDelete("/admin/sessions/{sessionId}", async (string sessionId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} revoking session {SessionId}", session.User.Id, sessionId);

    var revoked = await sessionManagement.RevokeSessionAsync(sessionId, ct);
    if (!revoked)
    {
        return Results.NotFound(new { error = "Session not found or already revoked" });
    }

    logger.LogInformation("Session {SessionId} revoked successfully", sessionId);
    return Results.Json(new { ok = true });
});

group.MapDelete("/admin/users/{userId}/sessions", async (string userId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} revoking all sessions for user {UserId}", session.User.Id, userId);

    var count = await sessionManagement.RevokeAllUserSessionsAsync(userId, ct);

    logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
    return Results.Json(new { ok = true, revokedCount = count });
});

// ADMIN-02: Analytics dashboard endpoints
group.MapGet("/admin/analytics", async (
    HttpContext context,
    IAdminStatsService statsService,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int days = 30,
    string? gameId = null,
    string? roleFilter = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var queryParams = new AnalyticsQueryParams(fromDate, toDate, days, gameId, roleFilter);
    var stats = await statsService.GetDashboardStatsAsync(queryParams, ct);
    return Results.Ok(stats);
})
.WithName("GetAnalytics")
.WithTags("Admin")
.WithDescription("Get analytics dashboard statistics with metrics and time-series trends")
.Produces<DashboardStatsDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

group.MapPost("/admin/analytics/export", async (
    HttpContext context,
    IAdminStatsService statsService,
    ExportDataRequest request,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var data = await statsService.ExportDashboardDataAsync(request, ct);
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
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ExportAnalytics")
.WithTags("Admin")
.WithDescription("Export analytics dashboard data in CSV or JSON format")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// N8N-05: Workflow error logging endpoints

// Webhook endpoint for n8n (no authentication required for simplicity)
group.MapPost("/logs/workflow-error", async (
    IWorkflowErrorLoggingService errorLoggingService,
    LogWorkflowErrorRequest request,
    CancellationToken ct = default) =>
{
    await errorLoggingService.LogErrorAsync(request, ct);
    return Results.Ok(new { message = "Error logged successfully" });
})
.WithName("LogWorkflowError")
.WithTags("N8N")
.WithDescription("Webhook endpoint for n8n to log workflow errors (no auth required)")
.Produces(StatusCodes.Status200OK)
.Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

// Admin endpoint to list workflow errors
group.MapGet("/admin/workflows/errors", async (
    HttpContext context,
    IWorkflowErrorLoggingService errorLoggingService,
    string? workflowId = null,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int page = 1,
    int limit = 20,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var queryParams = new WorkflowErrorsQueryParams(workflowId, fromDate, toDate, page, limit);
    var errors = await errorLoggingService.GetErrorsAsync(queryParams, ct);
    return Results.Ok(errors);
})
.WithName("GetWorkflowErrors")
.WithTags("Admin")
.WithDescription("Get workflow errors with filtering and pagination (admin only)")
.Produces<PagedResult<WorkflowErrorDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Admin endpoint to get specific workflow error
group.MapGet("/admin/workflows/errors/{id:guid}", async (
    HttpContext context,
    IWorkflowErrorLoggingService errorLoggingService,
    Guid id,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var error = await errorLoggingService.GetErrorByIdAsync(id, ct);

    if (error == null)
    {
        return Results.NotFound(new { error = "Workflow error not found" });
    }

    return Results.Ok(error);
})
.WithName("GetWorkflowErrorById")
.WithTags("Admin")
.WithDescription("Get a specific workflow error by ID (admin only)")
.Produces<WorkflowErrorDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// OPS-07: Alerting system endpoints

// Prometheus AlertManager webhook endpoint (no auth - called by Prometheus)
group.MapPost("/alerts/prometheus", async (
    IAlertingService alertingService,
    PrometheusAlertWebhook webhook,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    logger.LogInformation(
        "Received Prometheus webhook: {Status}, {AlertCount} alerts",
        webhook.Status,
        webhook.Alerts.Length);

    foreach (var alert in webhook.Alerts)
    {
        try
        {
            if (alert.Status == "firing")
            {
                var metadata = new Dictionary<string, object>
                {
                    ["labels"] = alert.Labels,
                    ["annotations"] = alert.Annotations,
                    ["starts_at"] = alert.StartsAt,
                    ["group_key"] = webhook.GroupKey
                };

                await alertingService.SendAlertAsync(
                    alertType: alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                    severity: alert.Labels.GetValueOrDefault("severity", "warning"),
                    message: alert.Annotations.GetValueOrDefault("summary", "Alert triggered"),
                    metadata: metadata,
                    cancellationToken: ct);
            }
            else if (alert.Status == "resolved")
            {
                var alertType = alert.Labels.GetValueOrDefault("alertname", "Unknown");
                await alertingService.ResolveAlertAsync(alertType, ct);
            }
        }
        catch (Exception ex)
        {
            // Resilience pattern: Individual alert processing failure shouldn't break other alerts
            // Fail-open to ensure one malformed alert doesn't prevent processing remaining alerts
            logger.LogError(ex, "Error processing Prometheus alert: {AlertName}",
                alert.Labels.GetValueOrDefault("alertname", "Unknown"));
        }
    }

    return Results.Ok(new { message = "Webhook processed successfully" });
})
.WithName("PrometheusAlertWebhook")
.WithTags("Alerting")
.WithDescription("Webhook endpoint for Prometheus AlertManager (no auth required)")
.Produces(StatusCodes.Status200OK);

// Admin endpoint to get active alerts
group.MapGet("/admin/alerts", async (
    HttpContext context,
    IAlertingService alertingService,
    bool? activeOnly = true,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var alerts = activeOnly == true
        ? await alertingService.GetActiveAlertsAsync(ct)
        : await alertingService.GetAlertHistoryAsync(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            ct);

    return Results.Ok(alerts);
})
.WithName("GetAlerts")
.WithTags("Admin", "Alerting")
.WithDescription("Get active alerts or recent alert history (admin only)")
.Produces<List<AlertDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Admin endpoint to manually resolve an alert
group.MapPost("/admin/alerts/{alertType}/resolve", async (
    HttpContext context,
    IAlertingService alertingService,
    string alertType,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var resolved = await alertingService.ResolveAlertAsync(alertType, ct);

    if (!resolved)
    {
        return Results.NotFound(new { error = $"No active alerts found for type '{alertType}'" });
    }

    return Results.Ok(new { message = $"Alert '{alertType}' resolved successfully" });
})
.WithName("ResolveAlert")
.WithTags("Admin", "Alerting")
.WithDescription("Manually resolve an alert by type (admin only)")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ADMIN-01: Prompt Management endpoints

// List all prompt templates with pagination
group.MapGet("/admin/prompts", async (
    HttpContext context,
    MeepleAiDbContext db,
    int page = 1,
    int limit = 50,
    string? category = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = db.Set<PromptTemplateEntity>()
        .AsNoTracking()
        .Include(t => t.CreatedBy)
        .Include(t => t.Versions)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(category))
    {
        query = query.Where(t => t.Category == category);
    }

    var total = await query.CountAsync(ct);
    var templates = await query
        .OrderByDescending(t => t.CreatedAt)
        .Skip((page - 1) * limit)
        .Take(limit)
        .Select(t => new PromptTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Category = t.Category,
            CreatedByUserId = t.CreatedByUserId,
            CreatedByEmail = t.CreatedBy.Email,
            CreatedAt = t.CreatedAt,
            VersionCount = t.Versions.Count,
            ActiveVersionNumber = t.Versions.FirstOrDefault(v => v.IsActive) != null
                ? t.Versions.First(v => v.IsActive).VersionNumber
                : null
        })
        .ToListAsync(ct);

    return Results.Ok(new PagedResult<PromptTemplateDto>(templates, total, page, limit));
})
.WithName("ListPromptTemplates")
.WithTags("Admin", "PromptManagement")
.WithDescription("List all prompt templates with pagination and filtering (admin only)")
.Produces<PagedResult<PromptTemplateDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Create new prompt template
group.MapPost("/admin/prompts", async (
    CreatePromptTemplateRequest request,
    HttpContext context,
    MeepleAiDbContext db,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var exists = await db.Set<PromptTemplateEntity>()
        .AnyAsync(t => t.Name == request.Name, ct);

    if (exists)
    {
        return Results.BadRequest(new { error = $"Template with name '{request.Name}' already exists" });
    }

    var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
    if (user == null)
    {
        return Results.BadRequest(new { error = "User not found" });
    }

    var template = new PromptTemplateEntity
    {
        Id = Guid.NewGuid().ToString(),
        Name = request.Name,
        Description = request.Description,
        Category = request.Category,
        CreatedByUserId = session.User.Id,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = user
    };

    db.Set<PromptTemplateEntity>().Add(template);
    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/v1/admin/prompts/{template.Id}",
        new PromptTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            CreatedByUserId = template.CreatedByUserId,
            CreatedByEmail = user.Email,
            CreatedAt = template.CreatedAt,
            VersionCount = 0,
            ActiveVersionNumber = null
        });
})
.WithName("CreatePromptTemplate")
.WithTags("Admin", "PromptManagement")
.WithDescription("Create a new prompt template (admin only)")
.Produces<PromptTemplateDto>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Get template details with versions
group.MapGet("/admin/prompts/{id}", async (
    string id,
    HttpContext context,
    MeepleAiDbContext db,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var template = await db.Set<PromptTemplateEntity>()
        .AsNoTracking()
        .Include(t => t.CreatedBy)
        .Include(t => t.Versions)
        .FirstOrDefaultAsync(t => t.Id == id, ct);

    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    return Results.Ok(new PromptTemplateDto
    {
        Id = template.Id,
        Name = template.Name,
        Description = template.Description,
        Category = template.Category,
        CreatedByUserId = template.CreatedByUserId,
        CreatedByEmail = template.CreatedBy.Email,
        CreatedAt = template.CreatedAt,
        VersionCount = template.Versions.Count,
        ActiveVersionNumber = template.Versions.FirstOrDefault(v => v.IsActive)?.VersionNumber
    });
})
.WithName("GetPromptTemplate")
.WithTags("Admin", "PromptManagement")
.WithDescription("Get template details by ID (admin only)")
.Produces<PromptTemplateDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Create new version for a template
group.MapPost("/admin/prompts/{id}/versions", async (
    string id,
    CreatePromptVersionRequest request,
    HttpContext context,
    MeepleAiDbContext db,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var template = await db.Set<PromptTemplateEntity>()
        .Include(t => t.Versions)
        .Include(t => t.CreatedBy)
        .FirstOrDefaultAsync(t => t.Id == id, ct);

    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
    if (user == null)
    {
        return Results.BadRequest(new { error = "User not found" });
    }

    var nextVersionNumber = template.Versions.Any()
        ? template.Versions.Max(v => v.VersionNumber) + 1
        : 1;

    var version = new PromptVersionEntity
    {
        Id = Guid.NewGuid().ToString(),
        TemplateId = id,
        VersionNumber = nextVersionNumber,
        Content = request.Content,
        IsActive = false, // New versions are inactive by default
        CreatedByUserId = session.User.Id,
        CreatedAt = DateTime.UtcNow,
        Metadata = request.Metadata,
        Template = template,
        CreatedBy = user
    };

    db.Set<PromptVersionEntity>().Add(version);
    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/v1/admin/prompts/{id}/versions/{version.Id}",
        new PromptVersionDto
        {
            Id = version.Id,
            TemplateId = version.TemplateId,
            VersionNumber = version.VersionNumber,
            Content = version.Content,
            IsActive = version.IsActive,
            CreatedByUserId = version.CreatedByUserId,
            CreatedByEmail = user.Email,
            CreatedAt = version.CreatedAt,
            Metadata = version.Metadata
        });
})
.WithName("CreatePromptVersion")
.WithTags("Admin", "PromptManagement")
.WithDescription("Create a new version for a template (admin only)")
.Produces<PromptVersionDto>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Get version history for a template
group.MapGet("/admin/prompts/{id}/versions", async (
    string id,
    HttpContext context,
    MeepleAiDbContext db,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var template = await db.Set<PromptTemplateEntity>()
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == id, ct);

    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    var versions = await db.Set<PromptVersionEntity>()
        .AsNoTracking()
        .Include(v => v.CreatedBy)
        .Where(v => v.TemplateId == id)
        .OrderByDescending(v => v.VersionNumber)
        .Select(v => new PromptVersionDto
        {
            Id = v.Id,
            TemplateId = v.TemplateId,
            VersionNumber = v.VersionNumber,
            Content = v.Content,
            IsActive = v.IsActive,
            CreatedByUserId = v.CreatedByUserId,
            CreatedByEmail = v.CreatedBy.Email,
            CreatedAt = v.CreatedAt,
            Metadata = v.Metadata
        })
        .ToListAsync(ct);

    return Results.Ok(versions);
})
.WithName("GetPromptVersionHistory")
.WithTags("Admin", "PromptManagement")
.WithDescription("Get version history for a template (admin only)")
.Produces<List<PromptVersionDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// Activate a specific version (CRITICAL endpoint)
group.MapPost("/admin/prompts/{id}/versions/{versionId}/activate", async (
    string id,
    string versionId,
    HttpContext context,
    IPromptTemplateService promptService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var activated = await promptService.ActivateVersionAsync(id, versionId, session.User.Id, ct);

        if (!activated)
        {
            return Results.NotFound(new { error = "Version not found" });
        }

        return Results.Ok(new { message = "Version activated successfully" });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (PromptManagementService)
        return Results.Problem(
            statusCode: 500,
            title: "Activation Failed",
            detail: ex.Message);
    }
})
.WithName("ActivatePromptVersion")
.WithTags("Admin", "PromptManagement")
.WithDescription("Activate a specific prompt version with transaction safety and cache invalidation (admin only)")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

// ADMIN-01 Phase 4: Prompt Evaluation & Testing endpoints

// Evaluate a prompt version
group.MapPost("/admin/prompts/{templateId}/versions/{versionId}/evaluate", async (
    string templateId,
    string versionId,
    EvaluatePromptRequest request,
    IPromptEvaluationService evaluationService,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} evaluating prompt template {TemplateId}, version {VersionId}",
            session.User.Id, templateId, versionId);

        var result = await evaluationService.EvaluateAsync(
            templateId,
            versionId,
            request.DatasetPath,
            progressCallback: null,
            ct);

        if (request.StoreResults)
        {
            await evaluationService.StoreResultsAsync(result, ct);
            logger.LogInformation("Evaluation result {EvaluationId} stored to database", result.EvaluationId);
        }

        return Results.Ok(result);
    }
    catch (FileNotFoundException ex)
    {
        logger.LogWarning("Dataset not found: {Error}", ex.Message);
        return Results.NotFound(new { error = $"Dataset not found: {ex.Message}" });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (PromptEvaluationService)
        logger.LogError(ex, "Evaluation failed for template {TemplateId}, version {VersionId}", templateId, versionId);
        return Results.Problem($"Evaluation failed: {ex.Message}");
    }
})
.WithName("EvaluatePromptVersion")
.WithTags("Admin", "PromptManagement", "Testing")
.WithDescription("Run automated evaluation tests on a prompt version (admin only)")
.Produces<PromptEvaluationResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status500InternalServerError);

// Compare two prompt versions (A/B testing)
group.MapPost("/admin/prompts/{templateId}/compare", async (
    string templateId,
    ComparePromptsRequest request,
    IPromptEvaluationService evaluationService,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation(
            "Admin {AdminId} comparing prompt versions: Baseline {BaselineId} vs Candidate {CandidateId}",
            session.User.Id, request.BaselineVersionId, request.CandidateVersionId);

        var comparison = await evaluationService.CompareVersionsAsync(
            templateId,
            request.BaselineVersionId,
            request.CandidateVersionId,
            request.DatasetPath,
            ct);

        logger.LogInformation("Comparison completed - Recommendation: {Recommendation}", comparison.Recommendation);

        return Results.Ok(comparison);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (PromptEvaluationService)
        logger.LogError(ex, "Comparison failed for template {TemplateId}", templateId);
        return Results.Problem($"Comparison failed: {ex.Message}");
    }
})
.WithName("ComparePromptVersions")
.WithTags("Admin", "PromptManagement", "Testing")
.WithDescription("A/B comparison of two prompt versions with recommendation (admin only)")
.Produces<PromptComparisonResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

// Get historical evaluation results
group.MapGet("/admin/prompts/{templateId}/evaluations", async (
    string templateId,
    int limit,
    IPromptEvaluationService evaluationService,
    HttpContext context,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var results = await evaluationService.GetHistoricalResultsAsync(templateId, limit, ct);
    return Results.Ok(results);
})
.WithName("GetEvaluationHistory")
.WithTags("Admin", "PromptManagement", "Testing")
.WithDescription("Get historical evaluation results for a template (admin only)")
.Produces<List<PromptEvaluationResult>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status403Forbidden);

// Generate evaluation report
group.MapGet("/admin/prompts/evaluations/{evaluationId}/report", async (
    string evaluationId,
    string format,
    IPromptEvaluationService evaluationService,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        // Get all historical results and find the specific evaluation
        var allResults = await evaluationService.GetHistoricalResultsAsync("", 1000, ct);
        var result = allResults.FirstOrDefault(r => r.EvaluationId == evaluationId);

        if (result == null)
        {
            return Results.NotFound(new { error = "Evaluation not found" });
        }

        var reportFormat = format?.ToLowerInvariant() == "json"
            ? ReportFormat.Json
            : ReportFormat.Markdown;

        var report = evaluationService.GenerateReport(result, reportFormat);

        var contentType = reportFormat == ReportFormat.Json
            ? "application/json"
            : "text/markdown";

        return Results.Content(report, contentType);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (PromptEvaluationService)
        logger.LogError(ex, "Failed to generate report for evaluation {EvaluationId}", evaluationId);
        return Results.Problem($"Report generation failed: {ex.Message}");
    }
})
.WithName("GetEvaluationReport")
.WithTags("Admin", "PromptManagement", "Testing")
.WithDescription("Generate evaluation report in Markdown or JSON format (admin only)")
.Produces<string>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// ADMIN-01: User management endpoints
group.MapGet("/admin/users", async (
    HttpContext context,
    UserManagementService userManagement,
    string? search = null,
    string? role = null,
    string? sortBy = null,
    string? sortOrder = "desc",
    int page = 1,
    int limit = 20,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await userManagement.GetUsersAsync(search, role, sortBy, sortOrder, page, limit, ct);
    return Results.Json(result);
})
.WithName("GetUsers")
.WithTags("Admin");

group.MapPost("/admin/users", async (
    CreateUserRequest request,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session.User.Id, request.Email);
        var user = await userManagement.CreateUserAsync(request, ct);
        logger.LogInformation("User {UserId} created successfully", user.Id);
        return Results.Created($"/api/v1/admin/users/{user.Id}", user);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
    {
        logger.LogWarning("Failed to create user: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateUser")
.WithTags("Admin");

group.MapPut("/admin/users/{id}", async (
    string id,
    UpdateUserRequest request,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} updating user {UserId}", session.User.Id, id);
        var user = await userManagement.UpdateUserAsync(id, request, ct);
        logger.LogInformation("User {UserId} updated successfully", id);
        return Results.Ok(user);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("User {UserId} not found: {Error}", id, ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update user {UserId}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateUser")
.WithTags("Admin");

group.MapDelete("/admin/users/{id}", async (
    string id,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} deleting user {UserId}", session.User.Id, id);
        await userManagement.DeleteUserAsync(id, session.User.Id, ct);
        logger.LogInformation("User {UserId} deleted successfully", id);
        return Results.NoContent();
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("User {UserId} not found: {Error}", id, ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to delete user {UserId}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("DeleteUser")
.WithTags("Admin");

// API-04: Admin API Key Management endpoint
group.MapDelete("/admin/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete API key without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} permanently deleting API key {KeyId}", session.User.Id, keyId);

    var success = await apiKeyManagement.DeleteApiKeyAsync(keyId, session.User.Id, ct);

    if (!success)
    {
        logger.LogWarning("API key {KeyId} not found for admin deletion", keyId);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} permanently deleted by admin {AdminId}", keyId, session.User.Id);
    return Results.NoContent();
});

// CONFIG-01: Configuration management endpoints (Admin only)
group.MapGet("/admin/configurations", async (
    HttpContext context,
    IConfigurationService configService,
    string? category = null,
    string? environment = null,
    bool activeOnly = true,
    int page = 1,
    int pageSize = 50,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await configService.GetConfigurationsAsync(category, environment, activeOnly, page, pageSize);
    return Results.Json(result);
})
.WithName("GetConfigurations")
.WithTags("Admin", "Configuration")
.Produces<PagedResult<SystemConfigurationDto>>();

group.MapGet("/admin/configurations/{id}", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await configService.GetConfigurationByIdAsync(id);
    return config != null ? Results.Json(config) : Results.NotFound();
})
.WithName("GetConfigurationById")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

group.MapGet("/admin/configurations/key/{key}", async (
    string key,
    HttpContext context,
    IConfigurationService configService,
    string? environment = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await configService.GetConfigurationByKeyAsync(key, environment);
    return config != null ? Results.Json(config) : Results.NotFound();
})
.WithName("GetConfigurationByKey")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

group.MapPost("/admin/configurations", async (
    CreateConfigurationRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating configuration {Key}", session.User.Id, request.Key);
        var config = await configService.CreateConfigurationAsync(request, session.User.Id);
        logger.LogInformation("Configuration {Key} created with ID {Id}", request.Key, config.Id);
        return Results.Created($"/api/v1/admin/configurations/{config.Id}", config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create configuration {Key}: {Error}", request.Key, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>(201)
.ProducesValidationProblem();

group.MapPut("/admin/configurations/{id}", async (
    string id,
    UpdateConfigurationRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} updating configuration {Id}", session.User.Id, id);
        var config = await configService.UpdateConfigurationAsync(id, request, session.User.Id);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for update", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} updated to version {Version}", id, config.Version);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update configuration {Id}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404)
.ProducesValidationProblem();

group.MapDelete("/admin/configurations/{id}", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} deleting configuration {Id}", session.User.Id, id);
    var success = await configService.DeleteConfigurationAsync(id);

    if (!success)
    {
        logger.LogWarning("Configuration {Id} not found for deletion", id);
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("Configuration {Id} deleted successfully", id);
    return Results.NoContent();
})
.WithName("DeleteConfiguration")
.WithTags("Admin", "Configuration")
.Produces(204)
.Produces(404);

group.MapPatch("/admin/configurations/{id}/toggle", async (
    string id,
    bool isActive,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} toggling configuration {Id} to {Status}",
        session.User.Id, id, isActive ? "active" : "inactive");

    var config = await configService.ToggleConfigurationAsync(id, isActive, session.User.Id);

    if (config == null)
    {
        logger.LogWarning("Configuration {Id} not found for toggle", id);
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("Configuration {Id} toggled to {Status}", id, config.IsActive ? "active" : "inactive");
    return Results.Json(config);
})
.WithName("ToggleConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

group.MapPost("/admin/configurations/bulk-update", async (
    BulkConfigurationUpdateRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} performing bulk update on {Count} configurations",
            session.User.Id, request.Updates.Count);

        var configs = await configService.BulkUpdateConfigurationsAsync(request, session.User.Id);

        logger.LogInformation("Bulk update completed successfully for {Count} configurations", configs.Count);
        return Results.Json(configs);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Bulk update failed: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("BulkUpdateConfigurations")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<SystemConfigurationDto>>()
.ProducesValidationProblem();

group.MapPost("/admin/configurations/validate", async (
    string key,
    string value,
    string valueType,
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value1) || value1 is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await configService.ValidateConfigurationAsync(key, value, valueType);
    return Results.Json(result);
})
.WithName("ValidateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<ConfigurationValidationResult>();

group.MapGet("/admin/configurations/export", async (
    string environment,
    HttpContext context,
    IConfigurationService configService,
    bool activeOnly = true,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var export = await configService.ExportConfigurationsAsync(environment, activeOnly);
    return Results.Json(export);
})
.WithName("ExportConfigurations")
.WithTags("Admin", "Configuration")
.Produces<ConfigurationExportDto>();

group.MapPost("/admin/configurations/import", async (
    ConfigurationImportRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} importing {Count} configurations",
            session.User.Id, request.Configurations.Count);

        var importedCount = await configService.ImportConfigurationsAsync(request, session.User.Id);

        logger.LogInformation("Successfully imported {Count} configurations", importedCount);
        return Results.Json(new { importedCount });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (ConfigurationService)
        logger.LogError(ex, "Failed to import configurations");
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ImportConfigurations")
.WithTags("Admin", "Configuration")
.Produces<object>()
.ProducesValidationProblem();

// CONFIG-05: Feature flags management endpoints (Admin only)
group.MapGet("/admin/features", async (
    HttpContext context,
    IFeatureFlagService featureFlags,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var flags = await featureFlags.GetAllFeatureFlagsAsync();
    return Results.Json(new { features = flags });
})
.WithName("GetAllFeatureFlags")
.WithTags("Admin", "FeatureFlags")
.WithDescription("List all feature flags with their current states (Admin only)")
.Produces<object>(200)
.Produces(401)
.Produces(403);

group.MapPut("/admin/features/{featureName}", async (
    string featureName,
    FeatureFlagUpdateRequest request,
    HttpContext context,
    IFeatureFlagService featureFlags,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        // Parse role from request if provided
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, out var parsedRole))
        {
            role = parsedRole;
        }

        if (request.Enabled)
        {
            await featureFlags.EnableFeatureAsync(featureName, role, session.User.Id);
            logger.LogInformation("Admin {UserId} enabled feature {FeatureName}{RoleInfo}",
                session.User.Id, featureName, role.HasValue ? $" for {role.Value}" : "");
        }
        else
        {
            await featureFlags.DisableFeatureAsync(featureName, role, session.User.Id);
            logger.LogInformation("Admin {UserId} disabled feature {FeatureName}{RoleInfo}",
                session.User.Id, featureName, role.HasValue ? $" for {role.Value}" : "");
        }

        return Results.Json(new { featureName, enabled = request.Enabled, role = request.Role });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (FeatureFlagService)
        logger.LogError(ex, "Failed to update feature flag {FeatureName}", featureName);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateFeatureFlag")
.WithTags("Admin", "FeatureFlags")
.WithDescription("Enable or disable a feature flag globally or for a specific role (Admin only)")
.Produces<object>(200)
.Produces(400)
.Produces(401)
.Produces(403);

group.MapGet("/admin/configurations/{id}/history", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    int limit = 20,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var history = await configService.GetConfigurationHistoryAsync(id, limit);
    return Results.Json(history);
})
.WithName("GetConfigurationHistory")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<ConfigurationHistoryDto>>();

group.MapPost("/admin/configurations/{id}/rollback/{version:int}", async (
    string id,
    int version,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} rolling back configuration {Id} to version {Version}",
            session.User.Id, id, version);

        var config = await configService.RollbackConfigurationAsync(id, version, session.User.Id);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for rollback", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} rolled back successfully", id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Rollback failed for configuration {Id}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("RollbackConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

group.MapGet("/admin/configurations/categories", async (
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var categories = await configService.GetCategoriesAsync();
    return Results.Json(categories);
})
.WithName("GetCategories")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<string>>();

group.MapPost("/admin/configurations/cache/invalidate", async (
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    string? key = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (key != null)
    {
        logger.LogInformation("Admin {AdminId} invalidating cache for configuration key {Key}", session.User.Id, key);
        await configService.InvalidateCacheAsync(key);
    }
    else
    {
        logger.LogInformation("Admin {AdminId} invalidating all configuration cache", session.User.Id);
        await configService.InvalidateCacheAsync();
    }

    return Results.Json(new { ok = true, message = key != null ? $"Cache invalidated for key: {key}" : "All configuration cache invalidated" });
})
.WithName("InvalidateConfigurationCache")
.WithTags("Admin", "Configuration")
.Produces<object>();

// PERF-03: Cache management endpoints
group.MapGet("/admin/cache/stats", async (HttpContext context, IAiResponseCacheService cacheService, string? gameId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var stats = await cacheService.GetCacheStatsAsync(gameId, ct);
        return Results.Json(stats);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (HybridCacheService)
        return Results.Problem(detail: $"Failed to retrieve cache stats: {ex.Message}", statusCode: 500);
    }
})
.WithName("GetCacheStats")
.WithDescription("Get cache statistics with optional game filter (Admin only)")
.WithTags("Admin", "Cache")
.Produces<CacheStats>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

group.MapDelete("/admin/cache/games/{gameId}", async (string gameId, HttpContext context, IAiResponseCacheService cacheService, MeepleAiDbContext dbContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // Validate game exists (but proceed with cache invalidation even if not - idempotent)
    var gameExists = await dbContext.Games.AnyAsync(g => g.Id == gameId, ct);
    if (!gameExists)
    {
        logger.LogWarning("Admin {AdminId} invalidating cache for non-existent game {GameId} (idempotent)", session.User.Id, gameId);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} invalidating cache for game {GameId}", session.User.Id, gameId);
        await cacheService.InvalidateGameAsync(gameId, ct);
        logger.LogInformation("Successfully invalidated cache for game {GameId}", gameId);
        return Results.Json(new { ok = true, message = $"Cache invalidated for game '{gameId}'" });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (HybridCacheService)
        logger.LogError(ex, "Failed to invalidate cache for game {GameId}", gameId);
        return Results.Problem(detail: $"Failed to invalidate cache: {ex.Message}", statusCode: 500);
    }
})
.WithName("InvalidateGameCache")
.WithDescription("Invalidate all cached responses for a specific game (Admin only)")
.WithTags("Admin", "Cache")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status500InternalServerError);

group.MapDelete("/admin/cache/tags/{tag}", async (string tag, HttpContext context, IAiResponseCacheService cacheService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(tag))
    {
        return Results.BadRequest(new { error = "tag is required" });
    }

    try
    {
        logger.LogInformation("Admin {AdminId} invalidating cache by tag {Tag}", session.User.Id, tag);
        await cacheService.InvalidateByCacheTagAsync(tag, ct);
        logger.LogInformation("Successfully invalidated cache by tag {Tag}", tag);
        return Results.Json(new { ok = true, message = $"Cache invalidated for tag '{tag}'" });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (HybridCacheService)
        logger.LogError(ex, "Failed to invalidate cache by tag {Tag}", tag);
        return Results.Problem(detail: $"Failed to invalidate cache: {ex.Message}", statusCode: 500);
    }
})
.WithName("InvalidateCacheByTag")
.WithDescription("Invalidate cache entries by tag (e.g., game:chess, pdf:abc123) (Admin only)")
.WithTags("Admin", "Cache")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

// AI-07: Prompt versioning and management endpoints

// Create prompt template (Admin only)
group.MapPost("/prompts", async (CreatePromptTemplateRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to create prompt template without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating prompt template '{TemplateName}'", session.User.Id, request.Name);
        var response = await promptManagement.CreatePromptTemplateAsync(request, session.User.Id, ct);
        logger.LogInformation("Prompt template {TemplateId} created successfully", response.Template.Id);
        return Results.Created($"/api/v1/prompts/{response.Template.Id}", response);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create prompt template: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid prompt template request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Create new version of prompt template (Admin only)
group.MapPost("/prompts/{templateId}/versions", async (string templateId, CreatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to create prompt version without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating new version for prompt template {TemplateId}", session.User.Id, templateId);
        var version = await promptManagement.CreatePromptVersionAsync(templateId, request, session.User.Id, ct);
        logger.LogInformation("Prompt version {VersionId} (v{VersionNumber}) created successfully", version.Id, version.VersionNumber);
        return Results.Created($"/api/v1/prompts/{templateId}/versions/{version.VersionNumber}", version);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create prompt version: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid prompt version request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get version history for prompt template (Admin only)
group.MapGet("/prompts/{templateId}/versions", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to view prompt version history without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var history = await promptManagement.GetVersionHistoryAsync(templateId, ct);
        return Results.Json(history);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to get version history: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
});

// Get active version of prompt template (Authenticated users)
group.MapGet("/prompts/{templateId}/versions/active", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Get template to retrieve name
    var template = await promptManagement.GetTemplateAsync(templateId, ct);
    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    var activeVersion = await promptManagement.GetActiveVersionAsync(template.Name, ct);
    if (activeVersion == null)
    {
        return Results.NotFound(new { error = "No active version found for this template" });
    }

    return Results.Json(activeVersion);
});

// Activate version (rollback capability) (Admin only)
group.MapPut("/prompts/{templateId}/versions/{versionId}/activate", async (string templateId, string versionId, ActivatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to activate prompt version without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} activating version {VersionId} for template {TemplateId}", session.User.Id, versionId, templateId);
        var activatedVersion = await promptManagement.ActivateVersionAsync(templateId, versionId, session.User.Id, request.Reason, ct);
        logger.LogInformation("Version {VersionId} (v{VersionNumber}) activated successfully", activatedVersion.Id, activatedVersion.VersionNumber);
        return Results.Json(activatedVersion);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to activate prompt version: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid activation request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get audit log for prompt template (Admin only)
group.MapGet("/prompts/{templateId}/audit-log", async (string templateId, int limit, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to view audit log without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var auditLog = await promptManagement.GetAuditLogAsync(templateId, limit, ct);
        return Results.Json(auditLog);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to get audit log: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid audit log request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// List all prompt templates (Admin only)
group.MapGet("/prompts", async (string? category, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to list prompt templates without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var templates = await promptManagement.ListTemplatesAsync(category, ct);
    return Results.Json(templates);
});

// Get specific prompt template (Admin only)
group.MapGet("/prompts/{templateId}", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to get prompt template without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var template = await promptManagement.GetTemplateAsync(templateId, ct);
    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    return Results.Json(template);
});

// API-04: API Key Management endpoints
group.MapPost("/api-keys", async (CreateApiKeyRequest request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.KeyName))
    {
        return Results.BadRequest(new { error = "Key name is required" });
    }

    logger.LogInformation("User {UserId} creating API key '{KeyName}'", session.User.Id, request.KeyName);

    try
    {
        var result = await apiKeyManagement.CreateApiKeyAsync(
            session.User.Id,
            request,
            ct);

        logger.LogInformation("API key '{KeyId}' created for user {UserId}", result.ApiKey.Id, session.User.Id);

        return Results.Created($"/api/v1/api-keys/{result.ApiKey.Id}", result);
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid API key creation request from user {UserId}: {Error}", session.User.Id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

group.MapGet("/api-keys", async (HttpContext context, ApiKeyManagementService apiKeyManagement, bool includeRevoked = false, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var result = await apiKeyManagement.ListApiKeysAsync(session.User.Id, includeRevoked, page, pageSize, ct);
    return Results.Json(result);
});

group.MapGet("/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var apiKey = await apiKeyManagement.GetApiKeyAsync(keyId, session.User.Id, ct);

    if (apiKey == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    return Results.Json(apiKey);
});

group.MapPut("/api-keys/{keyId}", async (string keyId, UpdateApiKeyRequest request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} updating API key {KeyId}", session.User.Id, keyId);

    var updated = await apiKeyManagement.UpdateApiKeyAsync(
        keyId,
        session.User.Id,
        request,
        ct);

    if (updated == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} updated by user {UserId}", keyId, session.User.Id);
    return Results.Json(updated);
});

group.MapDelete("/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} revoking API key {KeyId}", session.User.Id, keyId);

    var success = await apiKeyManagement.RevokeApiKeyAsync(keyId, session.User.Id, ct);

    if (!success)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} revoked by user {UserId}", keyId, session.User.Id);
    return Results.NoContent();
});

group.MapPost("/api-keys/{keyId}/rotate", async (string keyId, RotateApiKeyRequest? request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} rotating API key {KeyId}", session.User.Id, keyId);

    var result = await apiKeyManagement.RotateApiKeyAsync(
        keyId,
        session.User.Id,
        request ?? new RotateApiKeyRequest(),
        ct);

    if (result == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {OldKeyId} rotated to {NewKeyId} by user {UserId}", keyId, result.NewApiKey.Id, session.User.Id);
    return Results.Json(result);
});

group.MapGet("/api-keys/{keyId}/usage", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var usage = await apiKeyManagement.GetApiKeyUsageAsync(keyId, session.User.Id, ct);

    if (usage == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    return Results.Json(usage);
});

// CHESS-03: Chess knowledge indexing endpoints
group.MapPost("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

    var result = await chessService.IndexChessKnowledgeAsync(ct);

    if (!result.Success)
    {
        logger.LogError("Chess knowledge indexing failed: {Error}", result.ErrorMessage);
        return Results.BadRequest(new { error = result.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
        result.TotalKnowledgeItems, result.TotalChunks);

    return Results.Json(new
    {
        success = true,
        totalItems = result.TotalKnowledgeItems,
        totalChunks = result.TotalChunks,
        categoryCounts = result.CategoryCounts
    });
});

group.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

    var searchResult = await chessService.SearchChessKnowledgeAsync(q, limit ?? 5, ct);

    if (!searchResult.Success)
    {
        logger.LogError("Chess knowledge search failed: {Error}", searchResult.ErrorMessage);
        return Results.BadRequest(new { error = searchResult.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);

    return Results.Json(new
    {
        success = true,
        results = searchResult.Results.Select(r => new
        {
            score = r.Score,
            text = r.Text,
            page = r.Page,
            chunkIndex = r.ChunkIndex
        })
    });
});

group.MapDelete("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

    var success = await chessService.DeleteChessKnowledgeAsync(ct);

    if (!success)
    {
        logger.LogError("Chess knowledge deletion failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation("Chess knowledge deletion completed successfully");
    return Results.Json(new { success = true });
});

// UI-01: Chat management endpoints
        return group;
    }
}
