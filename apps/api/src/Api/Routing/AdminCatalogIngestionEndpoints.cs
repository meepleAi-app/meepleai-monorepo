using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for bulk catalog ingestion (Excel import, enrichment, export).
/// Issue: Admin Bulk Excel Import.
/// </summary>
internal static class AdminCatalogIngestionEndpoints
{
    public static RouteGroupBuilder MapAdminCatalogIngestionEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/admin/catalog-ingestion/excel-import
        // Upload an Excel file to create skeleton SharedGame entries
        group.MapPost("/excel-import", async (
            IFormFile file,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new ImportGamesFromExcelCommand(file, session!.User!.Id), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("BulkImportAdmin")
        .WithName("ExcelImport")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Import games from Excel file";
            operation.Description = "Parses .xlsx file, creates skeleton SharedGame entries. Deduplicates by title and BggId.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/enqueue-enrichment
        // Enqueue selected games for BGG enrichment
        group.MapPost("/enqueue-enrichment", async (
            EnqueueEnrichmentRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new EnqueueEnrichmentCommand(request.SharedGameIds, session!.User!.Id), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("EnqueueEnrichment")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Enqueue games for BGG enrichment";
            operation.Description = "Enqueues selected skeleton/failed games for BGG data enrichment.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/enqueue-all-skeletons
        // Enqueue all skeleton and failed games for enrichment
        group.MapPost("/enqueue-all-skeletons", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new EnqueueAllSkeletonsCommand(session!.User!.Id), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("EnqueueAllSkeletons")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Enqueue all skeleton games for enrichment";
            operation.Description = "Finds all games with Skeleton or Failed status and enqueues them for BGG enrichment.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/mark-complete
        // Mark enriched games as complete
        group.MapPost("/mark-complete", async (
            MarkCompleteRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new MarkGamesCompleteCommand(request.SharedGameIds), ct).ConfigureAwait(false);

            return Results.Ok(new { Completed = result });
        })
        .WithName("MarkGamesComplete")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Mark games as complete";
            operation.Description = "Transitions enriched games to Complete status (no PDF download needed).";
            return operation;
        });

        // GET /api/v1/admin/catalog-ingestion/excel-export
        // Export catalog as Excel file with optional filters
        group.MapGet("/excel-export", async (
            [FromQuery] string? status,
            [FromQuery] bool? hasPdf,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var statusFilter = !string.IsNullOrEmpty(status)
                ? status.Split(',').Select(s => Enum.Parse<GameDataStatus>(s, true)).ToList()
                : null;

            var bytes = await mediator.Send(
                new ExportGamesToExcelCommand(statusFilter, hasPdf), ct).ConfigureAwait(false);

            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "catalog-export.xlsx");
        })
        .WithName("ExcelExport")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Export catalog to Excel";
            operation.Description = "Exports shared games catalog as .xlsx with optional filters by status and PDF availability. Max 10,000 rows.";
            return operation;
        });

        return group;
    }
}
