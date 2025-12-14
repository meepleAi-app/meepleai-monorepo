using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Prompt template management and evaluation endpoints (Admin only).
/// Handles prompt template CRUD, versioning, activation, evaluation, A/B testing, and audit logging.
/// </summary>
public static class PromptManagementEndpoints
{
    public static RouteGroupBuilder MapPromptManagementEndpoints(this RouteGroupBuilder group)
    {
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
                ct).ConfigureAwait(false);

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
            ArgumentNullException.ThrowIfNull(request);

            var result = await mediator.Send(
                new CreatePromptTemplateCommand(
                    request.Name,
                    request.Description,
                    request.Category,
                    request.InitialContent,
                    request.Metadata,
                    session!.User!.Id),
                ct).ConfigureAwait(false);

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
                ct).ConfigureAwait(false);

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
                    session!.User!.Id),
                ct).ConfigureAwait(false);

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
                ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("GetPromptVersionHistory")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Get version history for a template (admin only)")
        .Produces<List<PromptVersionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Activate a specific version (CRITICAL endpoint - CQRS pattern with audit trail)
        group.MapPost("/admin/prompts/{id:guid}/versions/{versionId:guid}/activate", async (
            Guid id,
            Guid versionId,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Execute prompt activation via CQRS handler (with transaction and audit logging)
            var command = new ActivatePromptVersionCommand(
                TemplateId: id,
                VersionId: versionId,
                ActivatedByUserId: session!.User!.Id,
                Reason: "Admin activation via UI"
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(new { message = "Version activated successfully", version = result });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return Results.NotFound(new { error = "Version not found" });
            }
        })
        .WithName("ActivatePromptVersion")
        .WithTags("Admin", "PromptManagement")
        .WithDescription("Activate a specific prompt version with transaction safety, audit trail, and cache invalidation (admin only). Uses CQRS pattern for domain event support.")
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
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation("Admin {AdminId} evaluating prompt template {TemplateId}, version {VersionId}",
                session!.User!.Id, templateId, versionId);

            // ADMIN-01: Use CQRS pattern for prompt evaluation
            var command = new Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation.EvaluatePromptCommand
            {
                TemplateId = templateId,
                VersionId = versionId,
                DatasetPath = request.DatasetPath,
                StoreResults = request.StoreResults
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

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
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation(
                "Admin {AdminId} comparing prompt versions: Baseline {BaselineId} vs Candidate {CandidateId}",
                session!.User!.Id, request.BaselineVersionId, request.CandidateVersionId);

            // ADMIN-01: Use CQRS pattern for prompt comparison
            var command = new Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation.ComparePromptVersionsCommand
            {
                TemplateId = templateId,
                BaselineVersionId = request.BaselineVersionId,
                CandidateVersionId = request.CandidateVersionId,
                DatasetPath = request.DatasetPath
            };

            var comparison = await mediator.Send(command, ct).ConfigureAwait(false);

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
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // ADMIN-01: Use CQRS pattern for evaluation history
            var query = new Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation.GetEvaluationHistoryQuery
            {
                TemplateId = templateId,
                Limit = limit
            };

            var results = await mediator.Send(query, ct).ConfigureAwait(false);
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
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // ADMIN-01: Use CQRS pattern for report generation
            var reportFormat = string.Equals(format?.ToLowerInvariant(), "json"
, StringComparison.Ordinal) ? ReportFormat.Json
                : ReportFormat.Markdown;

            var query = new Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation.GenerateEvaluationReportQuery
            {
                EvaluationId = evaluationId,
                Format = reportFormat
            };

            try
            {
                var (report, contentType) = await mediator.Send(query, ct).ConfigureAwait(false);
                return Results.Content(report, contentType);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "Evaluation not found" });
            }
        })
        .WithName("GetEvaluationReport")
        .WithTags("Admin", "PromptManagement", "Testing")
        .WithDescription("Generate evaluation report in Markdown or JSON format (admin only)")
        .Produces<string>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // AI-07: Prompt versioning and management endpoints (legacy compatibility)

        // Get version history for prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}/versions", async (Guid templateId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.GetPromptVersionHistoryQuery(templateId.ToString());
            var history = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(history);
        });

        // Get active version of prompt template (Authenticated users)
        group.MapGet("/prompts/{templateId:guid}/versions/active", async (Guid templateId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Get template to retrieve name
            var templateQuery = new Api.BoundedContexts.Administration.Application.Queries.GetPromptTemplateQuery(templateId.ToString());
            var template = await mediator.Send(templateQuery, ct).ConfigureAwait(false);
            if (template == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            var activeQuery = new Api.BoundedContexts.Administration.Application.Queries.GetActivePromptVersionQuery(template.Name);
            var activeVersion = await mediator.Send(activeQuery, ct).ConfigureAwait(false);
            if (activeVersion == null)
            {
                return Results.NotFound(new { error = "No active version found for this template" });
            }

            return Results.Json(activeVersion);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Activate version (rollback capability) (Admin only)
        group.MapPut("/prompts/{templateId:guid}/versions/{versionId:guid}/activate", async (Guid templateId, Guid versionId, ActivatePromptVersionRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} activating version {VersionId} for template {TemplateId}", session!.User!.Id, versionId, templateId);
            var command = new Api.BoundedContexts.Administration.Application.Commands.ActivatePromptVersionCommand(templateId, versionId, session!.User!.Id, request.Reason);
            var activatedVersion = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Version {VersionId} (v{VersionNumber}) activated successfully", activatedVersion.Id, activatedVersion.VersionNumber);
            return Results.Json(activatedVersion);
        });

        // Get audit log for prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}/audit-log", async (Guid templateId, int limit, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.GetPromptAuditLogQuery(templateId.ToString(), limit);
            var auditLog = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(auditLog);
        });

        // List all prompt templates (Admin only)
        group.MapGet("/prompts", async (string? category, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.ListPromptTemplatesQuery(category);
            var templates = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(templates);
        });

        // Get specific prompt template (Admin only)
        group.MapGet("/prompts/{templateId:guid}", async (Guid templateId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.GetPromptTemplateQuery(templateId.ToString());
            var template = await mediator.Send(query, ct).ConfigureAwait(false);
            if (template == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            return Results.Json(template);
        });

        return group;
    }
}
