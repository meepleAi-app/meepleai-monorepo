using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// n8n workflow integration endpoints.
/// Handles n8n configuration, template management, workflow imports, and error logging.
/// </summary>
public static class WorkflowEndpoints
{
    public static RouteGroupBuilder MapWorkflowEndpoints(this RouteGroupBuilder group)
    {
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

            var configs = await mediator.Send(new GetAllN8nConfigsQuery(), ct).ConfigureAwait(false);
            return Results.Json(new { configs });
        });

        group.MapGet("/admin/n8n/{configId}", async (Guid configId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var config = await mediator.Send(new GetN8nConfigByIdQuery(configId), ct).ConfigureAwait(false);

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
            var apiKeyEncrypted = await encryptionService.EncryptAsync(request.ApiKey, "N8nApiKey").ConfigureAwait(false);

            var command = new CreateN8nConfigCommand(
                Name: request.Name,
                BaseUrl: request.BaseUrl,
                ApiKeyEncrypted: apiKeyEncrypted,
                CreatedByUserId: session!.User.Id,
                WebhookUrl: request.WebhookUrl
            );

            logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.Id, request.Name);
            var config = await mediator.Send(command, ct).ConfigureAwait(false);
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
                apiKeyEncrypted = await encryptionService.EncryptAsync(request.ApiKey, "N8nApiKey").ConfigureAwait(false);
            }

            var command = new UpdateN8nConfigCommand(
                ConfigId: configId,
                Name: request.Name,
                BaseUrl: request.BaseUrl,
                WebhookUrl: request.WebhookUrl,
                ApiKeyEncrypted: apiKeyEncrypted,
                IsActive: request.IsActive
            );

            logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session!.User.Id, configId);
            var config = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("n8n config {ConfigId} updated successfully", config.Id);
            return Results.Json(config);
        });

        group.MapDelete("/admin/n8n/{configId}", async (Guid configId, IMediator mediator, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new DeleteN8nConfigCommand(ConfigId: configId);

            logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session!.User.Id, configId);
            var deleted = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!deleted)
            {
                return Results.NotFound(new { error = "Configuration not found" });
            }

            logger.LogInformation("n8n config {ConfigId} deleted successfully", configId);
            return Results.Json(new { ok = true });
        });

        group.MapPost("/admin/n8n/{configId:guid}/test", async (Guid configId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session!.User.Id, configId);

            var command = new Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nConfig.TestN8nConnectionCommand
            {
                ConfigId = configId
            };
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("n8n config {ConfigId} test result: {Success}", configId, result.Success);
            return Results.Json(result);
        });

        // N8N-04: Workflow template endpoints (CQRS pattern)
        group.MapGet("/n8n/templates", async (
            string? category,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates.GetN8nTemplatesQuery
            {
                Category = category
            };
            var templates = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(templates);
        })
        .RequireAuthorization()
        .WithName("GetN8nTemplates")
        .WithTags("N8N")
        .WithDescription("Get all n8n workflow templates, optionally filtered by category");

        group.MapGet("/n8n/templates/{id}", async (
            string id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates.GetN8nTemplateByIdQuery
            {
                TemplateId = id
            };
            var template = await mediator.Send(query, ct).ConfigureAwait(false);
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
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("User {UserId} importing n8n template {TemplateId}", session!.User.Id, id);

            var command = new Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nTemplates.ImportN8nTemplateCommand
            {
                TemplateId = id,
                Parameters = request.Parameters,
                UserId = session.User.Id.ToString()
            };
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Template {TemplateId} imported successfully as workflow {WorkflowId}", id, result.WorkflowId);
            return Results.Ok(result);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("ImportN8nTemplate")
        .WithTags("N8N")
        .WithDescription("Import an n8n workflow template with parameter substitution");

        group.MapPost("/n8n/templates/validate", async (
            ValidateTemplateRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Extract session for role check (RequireAuthorization ensures authentication)
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates.ValidateN8nTemplateQuery
            {
                TemplateJson = request.TemplateJson
            };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ValidateN8nTemplate")
        .WithTags("N8N")
        .WithDescription("Validate n8n workflow template JSON structure (admin only)");

        // N8N-05: Workflow error logging endpoints

        // Webhook endpoint for n8n (no authentication required for simplicity)
        group.MapPost("/logs/workflow-error", async (
            IMediator mediator,
            LogWorkflowErrorRequest request,
            CancellationToken ct = default) =>
        {
            var command = new Api.BoundedContexts.WorkflowIntegration.Application.Commands.WorkflowErrors.LogWorkflowErrorCommand
            {
                WorkflowId = request.WorkflowId,
                ExecutionId = request.ExecutionId,
                ErrorMessage = request.ErrorMessage,
                NodeName = request.NodeName,
                RetryCount = request.RetryCount,
                StackTrace = request.StackTrace
            };
            await mediator.Send(command, ct).ConfigureAwait(false);
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
            IMediator mediator,
            string? workflowId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors.GetWorkflowErrorsQuery
            {
                WorkflowId = workflowId,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                Limit = limit
            };
            var errors = await mediator.Send(query, ct).ConfigureAwait(false);
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
            IMediator mediator,
            Guid id,
            CancellationToken ct = default) =>
        {
            var (authorized, session, authError) = context.RequireAdminSession();
            if (!authorized) return authError!;

            var query = new Api.BoundedContexts.WorkflowIntegration.Application.Queries.WorkflowErrors.GetWorkflowErrorByIdQuery
            {
                ErrorId = id
            };
            var error = await mediator.Send(query, ct).ConfigureAwait(false);

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

        return group;
    }
}