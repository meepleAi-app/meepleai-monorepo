using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Extensions;
using MediatR;
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
                        log.Id.ToString(),
                        log.UserId?.ToString(),
                        log.GameId?.ToString()
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
                .Select(u => new UserSearchResultDto(u.Id.ToString(), u.DisplayName ?? u.Email, u.Email))
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

        group.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(request.gameId))
            {
                return Results.BadRequest(new { error = "gameId is required" });
            }

            if (!Guid.TryParse(request.gameId, out var gameGuid))
            {
                return Results.BadRequest(new { error = "Invalid game ID format" });
            }

            logger.LogInformation("Admin {UserId} creating demo RuleSpec for game {GameId}", session!.User.Id, gameGuid);
            var command = new CreateDemoRuleSpecCommand(gameGuid);
            var specDto = await mediator.Send(command, ct);

            // Convert DTO to Model for backward compatibility
            var atoms = specDto.Atoms.Select(a => new RuleAtom(a.Id, a.Text, a.Section, a.Page, a.Line)).ToList();
            var spec = new RuleSpec(specDto.GameId.ToString(), specDto.Version, specDto.CreatedAt, atoms);

            return Results.Json(new { ok = true, spec });
        });

        // ADM-01: Admin dashboard endpoints
        group.MapGet("/admin/requests", async (HttpContext context, AiRequestLogService logService, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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

        group.MapGet("/admin/stats", async (HttpContext context, AiRequestLogService logService, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var stats = await logService.GetStatsAsync(startDate, endDate, userId, gameId, ct);
            // TODO: Add feedback stats query when AgentFeedbackService is migrated to CQRS

            return Results.Json(new
            {
                stats.TotalRequests,
                stats.AvgLatencyMs,
                stats.TotalTokens,
                stats.SuccessRate,
                stats.EndpointCounts
                // feedbackCounts = feedbackStats.OutcomeCounts,
                // totalFeedback = feedbackStats.TotalFeedback,
                // feedbackByEndpoint = feedbackStats.EndpointOutcomeCounts
            });
        });

        // ISSUE-962 (BGAI-020): LLM provider health monitoring
        group.MapGet("/admin/llm/health", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var healthStatus = await mediator.Send(new GetLlmHealthQuery(), ct);
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
            MeepleAiDbContext dbContext,
            int limit = 100,
            int offset = 0,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Convert DateTime parameters to UTC if they have Kind=Unspecified (from query string parsing)
            // PostgreSQL requires UTC DateTimes
            if (startDate.HasValue && startDate.Value.Kind == DateTimeKind.Unspecified)
                startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            if (endDate.HasValue && endDate.Value.Kind == DateTimeKind.Unspecified)
                endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);

            // Query low-quality responses with filters
            var query = dbContext.AiRequestLogs
                .AsNoTracking()
                .Where(log => log.IsLowQuality);

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
                    log.Id,
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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

        // ADM-02: n8n workflow configuration endpoints (DDD/CQRS)
        group.MapGet("/admin/n8n", async (IMediator mediator, HttpContext context, IFeatureFlagService featureFlags, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // CONFIG-05: Check if n8n integration feature is enabled
            if (!await featureFlags.IsEnabledAsync("Features.N8nIntegration"))
            {
                return Results.Json(
                    new { error = "feature_disabled", message = "n8n integration is currently disabled", featureName = "Features.N8nIntegration" },
                    statusCode: 403);
            }

            var configs = await mediator.Send(new GetAllN8nConfigsQuery(), ct);
            return Results.Json(new { configs });
        });

        group.MapGet("/admin/n8n/{configId}", async (Guid configId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var config = await mediator.Send(new GetN8nConfigByIdQuery(configId), ct);

            if (config == null)
            {
                return Results.NotFound(new { error = "Configuration not found" });
            }

            return Results.Json(config);
        });

        group.MapPost("/admin/n8n", async (CreateN8nConfigRequest request, IMediator mediator, IEncryptionService encryptionService, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Encrypt API key before storing (AUTH-06 pattern)
            var apiKeyEncrypted = await encryptionService.EncryptAsync(request.ApiKey, "N8nApiKey");

            var command = new CreateN8nConfigCommand(
                Name: request.Name,
                BaseUrl: request.BaseUrl,
                ApiKeyEncrypted: apiKeyEncrypted,
                CreatedByUserId: Guid.Parse(session.User.Id),
                WebhookUrl: request.WebhookUrl
            );

            logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.Id, request.Name);
            var config = await mediator.Send(command, ct);
            logger.LogInformation("n8n config {ConfigId} created successfully", config.Id);
            return Results.Json(config);
        });

        group.MapPut("/admin/n8n/{configId}", async (Guid configId, UpdateN8nConfigRequest request, IMediator mediator, IEncryptionService encryptionService, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Encrypt API key if provided (AUTH-06 pattern)
            string? apiKeyEncrypted = null;
            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                apiKeyEncrypted = await encryptionService.EncryptAsync(request.ApiKey, "N8nApiKey");
            }

            var command = new UpdateN8nConfigCommand(
                ConfigId: configId,
                Name: request.Name,
                BaseUrl: request.BaseUrl,
                WebhookUrl: request.WebhookUrl,
                ApiKeyEncrypted: apiKeyEncrypted,
                IsActive: request.IsActive
            );

            logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session.User.Id, configId);
            var config = await mediator.Send(command, ct);
            logger.LogInformation("n8n config {ConfigId} updated successfully", config.Id);
            return Results.Json(config);
        });

        group.MapDelete("/admin/n8n/{configId}", async (Guid configId, IMediator mediator, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new DeleteN8nConfigCommand(ConfigId: configId);

            logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session.User.Id, configId);
            var deleted = await mediator.Send(command, ct);

            if (!deleted)
            {
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("n8n config {ConfigId} deleted successfully", configId);
            return Results.Json(new { ok = true });
        });

        group.MapPost("/admin/n8n/{configId:guid}/test", async (Guid configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session.User.Id, configId);
            var result = await n8nService.TestConnectionAsync(configId.ToString(), ct);
            logger.LogInformation("n8n config {ConfigId} test result: {Success}", configId, result.Success);
            return Results.Json(result);
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("User {UserId} importing n8n template {TemplateId}", session.User.Id, id);
            var result = await templateService.ImportTemplateAsync(id, request.Parameters, session.User.Id, ct);
            logger.LogInformation("Template {TemplateId} imported successfully as workflow {WorkflowId}", id, result.WorkflowId);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ImportN8nTemplate")
        .WithTags("N8N")
        .WithDescription("Import an n8n workflow template with parameter substitution");

        group.MapPost("/n8n/templates/validate", (
            ValidateTemplateRequest request,
            N8nTemplateService templateService,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Extract session for role check (RequireAuthorization ensures authentication)
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var sessions = await sessionManagement.GetAllSessionsAsync(string.IsNullOrEmpty(userId) ? (Guid?)null : Guid.Parse(userId), limit, ct);
            return Results.Json(sessions);
        });

        group.MapDelete("/admin/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} revoking session {SessionId}", session.User.Id, sessionId);

            var revoked = await sessionManagement.RevokeSessionAsync(sessionId, ct);
            if (!revoked)
            {
                return Results.NotFound(new { error = "Session not found or already revoked" });
            }

            logger.LogInformation("Session {SessionId} revoked successfully", sessionId);
            return Results.Json(new { ok = true });
        });

        group.MapDelete("/admin/users/{userId:guid}/sessions", async (Guid userId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} revoking all sessions for user {UserId}", session.User.Id, userId);

            var count = await sessionManagement.RevokeAllUserSessionsAsync(userId, ct);

            logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
            return Results.Json(new { ok = true, revokedCount = count });
        });

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAdminStatsQuery(fromDate, toDate, days, gameId, roleFilter);
            var stats = await mediator.Send(query, ct);
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
            IMediator mediator,
            ExportDataRequest request,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new ExportStatsCommand(request.Format, request.FromDate, request.ToDate, request.GameId);
            var data = await mediator.Send(command, ct);
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            var (authorized, session, authError) = context.RequireAdminSession();
            if (!authorized) return authError!;

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            IMediator mediator,
            int page = 1,
            int limit = 50,
            string? category = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new GetPromptTemplatesQuery(page, limit, category),
                ct);

            return Results.Ok(new PagedResult<PromptTemplateDto>(result.Templates, result.TotalCount, page, limit));
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
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new CreatePromptTemplateCommand(
                    request.Name,
                    request.Description,
                    request.Category,
                    Guid.Parse(session.User.Id)),
                ct);

            return Results.Created(
                $"/api/v1/admin/prompts/{result.Id}",
                result);
        })
        .WithName("CreatePromptTemplate")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Create a new prompt template (admin only)")
        .Produces<PromptTemplateDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Get template details with versions
        group.MapGet("/admin/prompts/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new GetPromptTemplateByIdQuery(id),
                ct);

            if (result == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            return Results.Ok(result);
        })
        .WithName("GetPromptTemplate")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Get template details by ID (admin only)")
        .Produces<PromptTemplateDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Create new version for a template
        group.MapPost("/admin/prompts/{id:guid}/versions", async (
            Guid id,
            CreatePromptVersionRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new CreatePromptVersionCommand(
                    id,
                    request.Content,
                    request.Metadata,
                    Guid.Parse(session.User.Id)),
                ct);

            return Results.Created(
                $"/api/v1/admin/prompts/{id}/versions/{result.Id}",
                result);
        })
        .WithName("CreatePromptVersion")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Create a new version for a template (admin only)")
        .Produces<PromptVersionDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Get version history for a template
        group.MapGet("/admin/prompts/{id:guid}/versions", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new GetPromptVersionsQuery(id),
                ct);

            return Results.Ok(result);
        })
        .WithName("GetPromptVersionHistory")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Get version history for a template (admin only)")
        .Produces<List<PromptVersionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Activate a specific version (CRITICAL endpoint)
        group.MapPost("/admin/prompts/{id:guid}/versions/{versionId:guid}/activate", async (
            Guid id,
            Guid versionId,
            HttpContext context,
            IPromptTemplateService promptService,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var activated = await promptService.ActivateVersionAsync(id, versionId, Guid.Parse(session.User.Id), ct);

            if (!activated)
            {
                return Results.NotFound(new { error = "Version not found" });
            }

            return Results.Ok(new { message = "Version activated successfully" });
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
        })
        .WithName("ComparePromptVersions")
        .WithTags("Admin", "PromptManagement", "Testing")
        .WithDescription("A/B comparison of two prompt versions with recommendation (admin only)")
        .Produces<PromptComparisonResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status500InternalServerError);

        // Get historical evaluation results
        group.MapGet("/admin/prompts/{templateId:guid}/evaluations", async (
            Guid templateId,
            int limit,
            IPromptEvaluationService evaluationService,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var results = await evaluationService.GetHistoricalResultsAsync(templateId.ToString(), limit, ct);
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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
            IMediator mediator,
            string? search = null,
            string? role = null,
            string? sortBy = null,
            string? sortOrder = "desc",
            int page = 1,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAllUsersQuery(search, role, sortBy, sortOrder, page, limit);
            var result = await mediator.Send(query, ct);
            return Results.Json(result);
        })
        .WithName("GetUsers")
        .WithTags("Admin");

        group.MapPost("/admin/users", async (
            CreateUserRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session.User.Id, request.Email);
            var command = new CreateUserCommand(request.Email, request.Password, request.DisplayName, request.Role ?? "user");
            var user = await mediator.Send(command, ct);
            logger.LogInformation("User {UserId} created successfully", user.Id);
            return Results.Created($"/api/v1/admin/users/{user.Id}", user);
        })
        .WithName("CreateUser")
        .WithTags("Admin");

        group.MapPut("/admin/users/{id}", async (
            string id,
            UpdateUserRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} updating user {UserId}", session.User.Id, id);
            var command = new UpdateUserCommand(id, request.Email, request.DisplayName, request.Role);
            var user = await mediator.Send(command, ct);
            logger.LogInformation("User {UserId} updated successfully", id);
            return Results.Ok(user);
        })
        .WithName("UpdateUser")
        .WithTags("Admin");

        group.MapDelete("/admin/users/{id}", async (
            string id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} deleting user {UserId}", session.User.Id, id);
            var command = new DeleteUserCommand(id, session.User.Id);
            await mediator.Send(command, ct);
            logger.LogInformation("User {UserId} deleted successfully", id);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithTags("Admin");

        // API-04: Admin API Key Management endpoint
        group.MapDelete("/admin/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

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

        // This file contains the migrated configuration endpoints for AdminEndpoints.cs
        // Replace lines 1648-2217 in AdminEndpoints.cs with this content

        // CONFIG-01: Configuration management endpoints (Admin only) - MIGRATED TO CQRS
        group.MapGet("/admin/configurations", async (
            HttpContext context,
            IMediator mediator,
            string? category = null,
            string? environment = null,
            bool activeOnly = true,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAllConfigsQuery(category, environment, activeOnly, page, pageSize);
            var result = await mediator.Send(query, ct);
            return Results.Json(result);
        })
        .WithName("GetConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<PagedConfigurationResult>();

        group.MapGet("/admin/configurations/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigByIdQuery(id);
            var config = await mediator.Send(query, ct);
            return config != null ? Results.Json(config) : Results.NotFound();
        })
        .WithName("GetConfigurationById")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/key/{key}", async (
            string key,
            HttpContext context,
            IMediator mediator,
            string? environment = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigByKeyQuery(key);
            var config = await mediator.Send(query, ct);
            return config != null ? Results.Json(config) : Results.NotFound();
        })
        .WithName("GetConfigurationByKey")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations", async (
            CreateConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating configuration {Key}", session.User.Id, request.Key);
            var command = new CreateConfigurationCommand(
                Key: request.Key,
                Value: request.Value,
                ValueType: request.ValueType,
                CreatedByUserId: Guid.Parse(session.User.Id),
                Description: request.Description,
                Category: request.Category,
                Environment: request.Environment,
                RequiresRestart: request.RequiresRestart
            );
            var config = await mediator.Send(command, ct);
            logger.LogInformation("Configuration {Key} created with ID {Id}", request.Key, config.Id);
            return Results.Created($"/api/v1/admin/configurations/{config.Id}", config);
        })
        .WithName("CreateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>(201)
        .ProducesValidationProblem();

        group.MapPut("/admin/configurations/{id:guid}", async (
            Guid id,
            UpdateConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} updating configuration {Id}", session.User.Id, id);

            // For simplicity, we only support value updates via this endpoint
            if (request.Value == null)
            {
                return Results.BadRequest(new { error = "Value is required for update" });
            }

            var command = new UpdateConfigValueCommand(
                ConfigId: id,
                NewValue: request.Value,
                UpdatedByUserId: Guid.Parse(session.User.Id)
            );
            var config = await mediator.Send(command, ct);

            if (config == null)
            {
                logger.LogWarning("Configuration {Id} not found for update", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} updated to version {Version}", id, config.Version);
            return Results.Json(config);
        })
        .WithName("UpdateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404)
        .ProducesValidationProblem();

        group.MapDelete("/admin/configurations/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} deleting configuration {Id}", session.User.Id, id);
            var command = new DeleteConfigurationCommand(id);
            var success = await mediator.Send(command, ct);

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

        group.MapPatch("/admin/configurations/{id:guid}/toggle", async (
            Guid id,
            bool isActive,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} toggling configuration {Id} to {Status}",
                session.User.Id, id, isActive ? "active" : "inactive");

            var command = new ToggleConfigurationCommand(id, isActive);
            var config = await mediator.Send(command, ct);

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
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapPost("/admin/configurations/bulk-update", async (
            BulkConfigurationUpdateRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} performing bulk update on {Count} configurations",
                session.User.Id, request.Updates.Count);

            var updates = request.Updates.Select(u => new BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationUpdate(
                Id: Guid.Parse(u.Id),
                Value: u.Value
            )).ToList();

            var command = new BulkUpdateConfigsCommand(updates, Guid.Parse(session.User.Id));
            var configs = await mediator.Send(command, ct);

            logger.LogInformation("Bulk update completed successfully for {Count} configurations", configs.Count);
            return Results.Json(configs);
        })
        .WithName("BulkUpdateConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<ConfigurationDto>>()
        .ProducesValidationProblem();

        group.MapPost("/admin/configurations/validate", async (
            string key,
            string value,
            string valueType,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new ValidateConfigCommand(key, value, valueType);
            var result = await mediator.Send(command, ct);
            return Results.Json(result);
        })
        .WithName("ValidateConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationValidationResult>();

        group.MapGet("/admin/configurations/export", async (
            string environment,
            HttpContext context,
            IMediator mediator,
            bool activeOnly = true,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new ExportConfigsQuery(environment, activeOnly);
            var export = await mediator.Send(query, ct);
            return Results.Json(export);
        })
        .WithName("ExportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationExportDto>();

        group.MapPost("/admin/configurations/import", async (
            ConfigurationImportRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} importing {Count} configurations",
                session.User.Id, request.Configurations.Count);

            var items = request.Configurations.Select(c => new ConfigurationImportItem(
                Key: c.Key,
                Value: c.Value,
                ValueType: c.ValueType,
                Description: c.Description,
                Category: c.Category,
                IsActive: c.IsActive,
                RequiresRestart: c.RequiresRestart,
                Environment: c.Environment
            )).ToList();

            var command = new ImportConfigsCommand(items, request.OverwriteExisting, Guid.Parse(session.User.Id));
            var importedCount = await mediator.Send(command, ct);

            logger.LogInformation("Successfully imported {Count} configurations", importedCount);
            return Results.Json(new { importedCount });
        })
        .WithName("ImportConfigurations")
        .WithTags("Admin", "Configuration")
        .Produces<object>()
        .ProducesValidationProblem();

        group.MapGet("/admin/configurations/{id:guid}/history", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigHistoryQuery(id, limit);
            var history = await mediator.Send(query, ct);
            return Results.Json(history);
        })
        .WithName("GetConfigurationHistory")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<Api.BoundedContexts.SystemConfiguration.Application.Queries.ConfigurationHistoryDto>>();

        group.MapPost("/admin/configurations/{id:guid}/rollback/{version:int}", async (
            Guid id,
            int version,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} rolling back configuration {Id} to version {Version}",
                session.User.Id, id, version);

            var command = new RollbackConfigCommand(id, version, Guid.Parse(session.User.Id));
            var config = await mediator.Send(command, ct);

            if (config == null)
            {
                logger.LogWarning("Configuration {Id} not found for rollback", id);
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("Configuration {Id} rolled back successfully", id);
            return Results.Json(config);
        })
        .WithName("RollbackConfiguration")
        .WithTags("Admin", "Configuration")
        .Produces<ConfigurationDto>()
        .Produces(404);

        group.MapGet("/admin/configurations/categories", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetConfigCategoriesQuery();
            var categories = await mediator.Send(query, ct);
            return Results.Json(categories);
        })
        .WithName("GetCategories")
        .WithTags("Admin", "Configuration")
        .Produces<IReadOnlyList<string>>();

        group.MapPost("/admin/configurations/cache/invalidate", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            string? key = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (key != null)
            {
                logger.LogInformation("Admin {AdminId} invalidating cache for configuration key {Key}", session.User.Id, key);
            }
            else
            {
                logger.LogInformation("Admin {AdminId} invalidating all configuration cache", session.User.Id);
            }

            var command = new InvalidateCacheCommand(key);
            await mediator.Send(command, ct);

            return Results.Json(new { ok = true, message = key != null ? $"Cache invalidated for key: {key}" : "All configuration cache invalidated" });
        })
        .WithName("InvalidateConfigurationCache")
        .WithTags("Admin", "Configuration")
        .Produces<object>();

        // PERF-03: Cache management endpoints
        group.MapGet("/admin/cache/stats", async (HttpContext context, IAiResponseCacheService cacheService, string? gameId = null, CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var stats = await cacheService.GetCacheStatsAsync(gameId, ct);
            return Results.Json(stats);
        })
        .WithName("GetCacheStats")
        .WithDescription("Get cache statistics with optional game filter (Admin only)")
        .WithTags("Admin", "Cache")
        .Produces<CacheStats>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapDelete("/admin/cache/games/{gameId:guid}", async (Guid gameId, HttpContext context, IAiResponseCacheService cacheService, MeepleAiDbContext dbContext, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Validate game exists (but proceed with cache invalidation even if not - idempotent)
            var gameExists = await dbContext.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, ct);
            if (!gameExists)
            {
                logger.LogWarning("Admin {AdminId} invalidating cache for non-existent game {GameId} (idempotent)", session.User.Id, gameId);
            }

            logger.LogInformation("Admin {AdminId} invalidating cache for game {GameId}", session.User.Id, gameId);
            await cacheService.InvalidateGameAsync(gameId.ToString(), ct);
            logger.LogInformation("Successfully invalidated cache for game {GameId}", gameId);
            return Results.Json(new { ok = true, message = $"Cache invalidated for game '{gameId}'" });
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest(new { error = "tag is required" });
            }

            logger.LogInformation("Admin {AdminId} invalidating cache by tag {Tag}", session.User.Id, tag);
            await cacheService.InvalidateByCacheTagAsync(tag, ct);
            logger.LogInformation("Successfully invalidated cache by tag {Tag}", tag);
            return Results.Json(new { ok = true, message = $"Cache invalidated for tag '{tag}'" });
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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating prompt template '{TemplateName}'", session.User.Id, request.Name);
            var response = await promptManagement.CreatePromptTemplateAsync(request, Guid.Parse(session.User.Id), ct);
            logger.LogInformation("Prompt template {TemplateId} created successfully", response.Template.Id);
            return Results.Created($"/api/v1/prompts/{response.Template.Id}", response);
        });

        // Create new version of prompt template (Admin only)
        group.MapPost("/prompts/{templateId:guid}/versions", async (Guid templateId, CreatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} creating new version for prompt template {TemplateId}", session.User.Id, templateId);
            var version = await promptManagement.CreatePromptVersionAsync(templateId.ToString(), request, Guid.Parse(session.User.Id), ct);
            logger.LogInformation("Prompt version {VersionId} (v{VersionNumber}) created successfully", version.Id, version.VersionNumber);
            return Results.Created($"/api/v1/prompts/{templateId}/versions/{version.VersionNumber}", version);
        });

        // Get version history for prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}/versions", async (Guid templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var history = await promptManagement.GetVersionHistoryAsync(templateId.ToString(), ct);
            return Results.Json(history);
        });

        // Get active version of prompt template (Authenticated users)
        group.MapGet("/prompts/{templateId:guid}/versions/active", async (Guid templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Get template to retrieve name
            var template = await promptManagement.GetTemplateAsync(templateId.ToString(), ct);
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
        group.MapPut("/prompts/{templateId:guid}/versions/{versionId:guid}/activate", async (Guid templateId, Guid versionId, ActivatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} activating version {VersionId} for template {TemplateId}", session.User.Id, versionId, templateId);
            var activatedVersion = await promptManagement.ActivateVersionAsync(templateId.ToString(), versionId.ToString(), Guid.Parse(session.User.Id), request.Reason, ct);
            logger.LogInformation("Version {VersionId} (v{VersionNumber}) activated successfully", activatedVersion.Id, activatedVersion.VersionNumber);
            return Results.Json(activatedVersion);
        });

        // Get audit log for prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}/audit-log", async (Guid templateId, int limit, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var auditLog = await promptManagement.GetAuditLogAsync(templateId.ToString(), limit, ct);
            return Results.Json(auditLog);
        });

        // List all prompt templates (Admin only)
        group.MapGet("/prompts", async (string? category, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var templates = await promptManagement.ListTemplatesAsync(category, ct);
            return Results.Json(templates);
        });

        // Get specific prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}", async (Guid templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var template = await promptManagement.GetTemplateAsync(templateId.ToString(), ct);
            if (template == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            return Results.Json(template);
        });

        // API-04: API Key Management endpoints
        group.MapPost("/api-keys", async (CreateApiKeyRequest request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(request.KeyName))
            {
                return Results.BadRequest(new { error = "Key name is required" });
            }

            logger.LogInformation("User {UserId} creating API key '{KeyName}'", session.User.Id, request.KeyName);

            var result = await apiKeyManagement.CreateApiKeyAsync(
                session.User.Id,
                request,
                ct);

            logger.LogInformation("API key '{KeyId}' created for user {UserId}", result.ApiKey.Id, session.User.Id);

            return Results.Created($"/api/v1/api-keys/{result.ApiKey.Id}", result);
        });

        group.MapGet("/api-keys", async (HttpContext context, ApiKeyManagementService apiKeyManagement, bool includeRevoked = false, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var result = await apiKeyManagement.ListApiKeysAsync(session.User.Id, includeRevoked, page, pageSize, ct);
            return Results.Json(result);
        });

        group.MapGet("/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var usage = await apiKeyManagement.GetApiKeyUsageAsync(keyId, session.User.Id, ct);

            if (usage == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            return Results.Json(usage);
        });

        // CHESS-03: Chess knowledge indexing endpoints
        group.MapPost("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

            var result = await mediator.Send(new IndexChessKnowledgeCommand(), ct);

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

        group.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

            var searchResult = await mediator.Send(new SearchChessKnowledgeQuery { Query = q, Limit = limit ?? 5 }, ct);

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

        group.MapDelete("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

            var success = await mediator.Send(new DeleteChessKnowledgeCommand(), ct);

            if (!success)
            {
                logger.LogError("Chess knowledge deletion failed");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            logger.LogInformation("Chess knowledge deletion completed successfully");
            return Results.Json(new { success = true });
        });

        // ISSUE-960: BGAI-018 - LLM Cost Reporting
        group.MapGet("/llm-costs/report", async (
            HttpContext context,
            IMediator mediator,
            string? startDate,
            string? endDate,
            Guid? userId,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Default to last 30 days if not specified
            var start = string.IsNullOrWhiteSpace(startDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
                : DateOnly.Parse(startDate);

            var end = string.IsNullOrWhiteSpace(endDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(endDate);

            var query = new GetLlmCostReportQuery
            {
                StartDate = start,
                EndDate = end,
                UserId = userId
            };

            var report = await mediator.Send(query, ct);

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
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var targetDate = string.IsNullOrWhiteSpace(date)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(date);

            var query = new GetLlmCostReportQuery
            {
                StartDate = targetDate,
                EndDate = targetDate
            };

            var report = await mediator.Send(query, ct);

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
            LlmCostAlertService alertService,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Check all thresholds (daily, weekly, monthly projection)
            await alertService.CheckDailyCostThresholdAsync(ct);
            await alertService.CheckWeeklyCostThresholdAsync(ct);
            await alertService.CheckMonthlyCostProjectionAsync(ct);

            return Results.Json(new { success = true, message = "Cost threshold checks completed" });
        })
        .WithTags("Admin", "LLM", "Alerts")
        .WithName("CheckLlmCostAlerts");

        // UI-01: Chat management endpoints
        return group;
    }
}