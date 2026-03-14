using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.BulkUploadPdfs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetDocumentOverview;
using Api.Extensions;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for shared game content management.
/// Issues #117, #113: Bulk PDF upload and MAU monitoring.
/// </summary>
internal static class AdminSharedGameContentEndpoints
{
    public static RouteGroupBuilder MapAdminSharedGameContentEndpoints(this RouteGroupBuilder group)
    {
        var contentGroup = group.MapGroup("/admin/shared-games")
            .WithTags("Admin", "SharedGameContent")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /api/v1/admin/shared-games/{gameId}/documents/bulk-upload
        // Issue #117: Bulk PDF Upload
        contentGroup.MapPost("/{gameId:guid}/documents/bulk-upload", HandleBulkUploadPdfs)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(524_288_000)) // 500 MB for bulk uploads
            .WithName("BulkUploadPdfsForSharedGame")
            .WithSummary("Bulk upload PDFs for a shared game (Admin)")
            .WithDescription("Upload multiple PDF files at once for a shared game. Each file is processed independently.")
            .Produces<BulkUploadPdfsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/shared-games/{sharedGameId}/documents/overview
        // Issue #119: Per-SharedGame Document Overview
        contentGroup.MapGet("/{sharedGameId:guid}/documents/overview", async (
                Guid sharedGameId,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetDocumentOverviewQuery(sharedGameId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetSharedGameDocumentOverview")
            .WithSummary("Get consolidated document overview for a shared game (Admin)")
            .WithDescription("Returns processing status breakdown, per-document details, agent linkage, and RAG readiness assessment.")
            .Produces<DocumentOverviewResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/shared-games/mau
        // Issue #113: MAU Monitoring Dashboard
        var mauGroup = group.MapGroup("/admin/monitoring")
            .WithTags("Admin", "Monitoring")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        mauGroup.MapGet("/mau", HandleGetActiveAiUsers)
            .WithName("GetActiveAiUsers")
            .WithSummary("Get Monthly Active AI Users data (Admin)")
            .WithDescription("Returns MAU data with feature breakdown (AI chat, PDF uploads, agent interactions) and daily trend.")
            .Produces<ActiveAiUsersResult>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> HandleGetActiveAiUsers(
        [FromQuery] int period,
        IMediator mediator,
        CancellationToken ct)
    {
        if (period is not (7 or 30 or 90))
        {
            period = 30;
        }

        var query = new GetActiveAiUsersQuery(period);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkUploadPdfs(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var files = form.Files;

        if (files.Count == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", message = "No files provided" });
        }

        // Validate all files are PDFs
        var pdfFiles = new List<Microsoft.AspNetCore.Http.IFormFile>();
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    error = "validation_failed",
                    message = $"File '{file.FileName}' is not a PDF"
                });
            }

            pdfFiles.Add(file);
        }

        if (pdfFiles.Count == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", message = "No valid PDF files provided" });
        }

        var command = new BulkUploadPdfsCommand(
            SharedGameId: gameId,
            UserId: session!.User!.Id,
            Files: pdfFiles);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Bulk upload for game {GameId}: {Success}/{Total} succeeded",
            gameId, result.SuccessCount, result.TotalRequested);

        return Results.Ok(result);
    }
}
