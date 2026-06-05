using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Extensions;
using Api.Middleware.Exceptions;
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
                new ImportGamesFromExcelCommand(file, session!.Principal!.Subject.Id), ct).ConfigureAwait(false);

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
                new EnqueueEnrichmentCommand(request.SharedGameIds, session!.Principal!.Subject.Id), ct).ConfigureAwait(false);

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
                new EnqueueAllSkeletonsCommand(session!.Principal!.Subject.Id), ct).ConfigureAwait(false);

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

            List<GameDataStatus>? statusFilter = null;
            if (!string.IsNullOrEmpty(status))
            {
                statusFilter = new List<GameDataStatus>();
                foreach (var s in status.Split(','))
                {
                    if (!Enum.TryParse<GameDataStatus>(s.Trim(), true, out var parsed))
                        return Results.BadRequest(new { error = $"Invalid status value: '{s.Trim()}'" });
                    statusFilter.Add(parsed);
                }
            }

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

        // POST /api/v1/admin/catalog-ingestion/excel-preview
        // Upload Excel and get a diff preview (new/modified/removed games)
        group.MapPost("/excel-preview", async (
            IFormFile file,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new PreviewExcelImportQuery(file), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("BulkImportAdmin")
        .WithName("ExcelPreview")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Preview Excel import diff";
            operation.Description = "Parses .xlsx file, compares with existing catalog, returns categorized diff (new/modified/removed/unchanged). Does not modify data.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/excel-confirm
        // Apply confirmed Excel import changes (create/update/delete)
        group.MapPost("/excel-confirm", async (
            ConfirmExcelImportRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new ConfirmExcelImportCommand(request, session!.Principal!.Subject.Id), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("ExcelConfirm")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Confirm Excel import changes";
            operation.Description = "Applies previewed changes: creates new games, updates Draft games, soft-deletes removed Draft games. Published games are protected.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/assign-bgg-id
        // Assign a BGG ID to a skeleton game
        group.MapPost("/assign-bgg-id", async (
            AssignBggIdRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var success = await mediator.Send(
                new AssignBggIdCommand(request.SharedGameId, request.BggId, session!.Principal!.Subject.Id), ct)
                .ConfigureAwait(false);

            return success
                ? Results.Ok(new { assigned = true })
                : Results.BadRequest(new { assigned = false, error = "BGG ID already in use or game not found/eligible" });
        })
        .WithName("AssignBggId")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Assign BGG ID to skeleton game";
            operation.Description = "Sets BGG ID on a Skeleton/Failed game, enabling BGG enrichment. Rejects if BGG ID is already assigned to another game.";
            return operation;
        });

        // ============================================================
        // #1861 — Catalog sync run history (Phase 4)
        // ============================================================

        // GET /api/v1/admin/catalog-ingestion/status
        group.MapGet("/status", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetCatalogSyncStatusQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetCatalogSyncStatus")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Catalog sync status";
            operation.Description = "Returns current status (running|idle|never_run), last/current run summary, cumulative games total, and next scheduled timestamp.";
            return operation;
        });

        // GET /api/v1/admin/catalog-ingestion/runs?page=N&pageSize=M
        group.MapGet("/runs", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetCatalogSyncRunsQuery(page ?? 1, pageSize ?? 12);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetCatalogSyncRuns")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Catalog sync run history (paged)";
            operation.Description = "Returns recent catalog sync runs ordered by CreatedAt DESC. Defaults: page=1, pageSize=12. Bounds: pageSize 1-100.";
            return operation;
        });

        // GET /api/v1/admin/catalog-ingestion/runs/{id}/logs?tail=N
        group.MapGet("/runs/{id:guid}/logs", async (
            Guid id,
            [FromQuery] int? tail,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetCatalogSyncRunLogsQuery(id, tail ?? 100);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return result is null
                ? Results.NotFound(new { error = $"Catalog sync run {id} not found." })
                : Results.Ok(result);
        })
        .WithName("GetCatalogSyncRunLogs")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Catalog sync run logs (tail-N)";
            operation.Description = "Returns the last N log lines for the given run. 404 if run missing. LogsAvailable=false when no log file attached or unreadable.";
            return operation;
        });

        // POST /api/v1/admin/catalog-ingestion/trigger
        group.MapPost("/trigger", async (
            TriggerCatalogSyncRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            try
            {
                var command = new TriggerCatalogSyncCommand(
                    request.Provider, session!.Principal!.Subject.Id);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Accepted($"/api/v1/admin/catalog-ingestion/runs/{result.RunId}/logs", result);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireRateLimiting("BulkImportAdmin")
        .WithName("TriggerCatalogSync")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Trigger manual catalog sync";
            operation.Description = "Enqueues a new catalog sync run for the given provider. 202 Accepted with runId on success. 409 Conflict if a run is already in progress.";
            return operation;
        });

        // ============================================================
        // #1874 — Enrichment queue + failed items aggregator
        // ============================================================

        // GET /api/v1/admin/catalog-ingestion/enrichment-queue?priority=&limit=
        group.MapGet("/enrichment-queue", async (
            [FromQuery] EnrichmentPriority? priority,
            [FromQuery] int? limit,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetEnrichmentQueueQuery(priority, limit ?? 25);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetEnrichmentQueue")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Enrichment queue (pending entries)";
            operation.Description = "Returns queued BGG enrichment requests sorted by Priority DESC, QueuedAt ASC. Filters by priority when supplied. Default limit=25 (1-100).";
            return operation;
        });

        // GET /api/v1/admin/catalog-ingestion/failed-items?days=&limit=
        group.MapGet("/failed-items", async (
            [FromQuery] int? days,
            [FromQuery] int? limit,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetFailedItemsQuery(days ?? 30, limit ?? 50);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetFailedItems")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Failed enrichment items (aggregated)";
            operation.Description = "Returns the most recent failed enrichment attempt per shared game within the trailing N days. Default days=30 (1-365), default limit=50 (1-100).";
            return operation;
        });

        return group;
    }
}

/// <summary>
/// Request body for POST /api/v1/admin/catalog-ingestion/trigger (#1861).
/// </summary>
public sealed record TriggerCatalogSyncRequest(CatalogSyncProvider Provider);
