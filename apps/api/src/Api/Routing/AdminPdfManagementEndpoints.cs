using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for PDF management operations.
/// PDF Storage Management Hub: Phases 4-6 (bulk ops, maintenance, analytics).
/// </summary>
internal static class AdminPdfManagementEndpoints
{
    public static void MapAdminPdfManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/pdfs")
            .WithTags("Admin - PDF Management")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // Issue #1653: Admin KB document delete (agent-cascade + audit)
        group.MapDelete("/{pdfId:guid}", DeleteKbDocument)
            .WithName("DeleteKbDocument")
            .WithSummary("Delete a KB document (cascade: detach from agents, remove chunks/vectors/blob, audited)");

        // Phase 4: Bulk operations
        group.MapPost("/bulk/delete", BulkDelete)
            .WithName("BulkDeletePdfs")
            .WithSummary("Bulk delete PDF documents");

        // Phase 5: Maintenance operations
        group.MapPost("/{pdfId:guid}/reindex", ReindexDocument)
            .WithName("ReindexDocument")
            .WithSummary("Reindex a PDF document (delete chunks, reset to Pending)");

        // #3447 slice: seed image-table regions from a raw Unstructured hi_res JSON body
        group.MapPost("/{pdfId:guid}/seed-image-regions", SeedImageRegions)
            .WithName("SeedPdfImageRegions")
            .WithSummary("Seed image-table regions from a raw Unstructured hi_res JSON body (#3447 slice)");

        group.MapPost("/maintenance/purge-stale", PurgeStale)
            .WithName("PurgeStaleDocuments")
            .WithSummary("Mark documents stuck in processing (>24h) as failed");

        group.MapPost("/maintenance/cleanup-orphans", CleanupOrphans)
            .WithName("CleanupOrphans")
            .WithSummary("Delete orphaned text chunks referencing non-existent PDFs");

        // #3435 (SP1): admin-triggered batch — runs a hi_res pass over Ready, never-seeded PDFs and
        // seeds their image-table regions. Gated by PdfProcessing:ImageRegionSeeding:Enabled (default off).
        group.MapPost("/maintenance/seed-image-regions-batch", SeedImageRegionsBatch)
            .WithName("SeedImageRegionsBatch")
            .WithSummary("Run one batch of automatic image-region hi_res seeding (#3435, flag-gated)");

        // #3435 (SP2 router, DC-B): list the PDFs that qualify for the Metà-C VLM enrichment pass —
        // Ready, in-corpus, non-demo, with at least the configured image-region count. Read-only.
        group.MapGet("/maintenance/table-region-candidates", GetTableRegionCandidates)
            .WithName("GetTableRegionCandidates")
            .WithSummary("List table-heavy PDFs (candidate image-table regions) for VLM enrichment (#3435 SP2)");

        // Phase 6: Analytics
        group.MapGet("/analytics/distribution", GetDistribution)
            .WithName("GetPdfStatusDistribution")
            .WithSummary("Get PDF status distribution for analytics");
    }

    private static async Task<IResult> BulkDelete(
        BulkDeletePdfsRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new BulkDeletePdfsCommand(request.PdfIds);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReindexDocument(
        Guid pdfId,
        ReindexDocumentRequest? request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new ReindexDocumentCommand(pdfId, request?.IndexerVersion),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { success = true, message = "Document queued for reindexing" });
    }

    private static async Task<IResult> SeedImageRegions(
        Guid pdfId,
        SeedImageRegionsRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var count = await mediator.Send(
            new SeedPdfImageRegionsCommand(pdfId, request.HiResJson, request.MinAreaFraction),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { success = true, seeded = count });
    }

    private static async Task<IResult> SeedImageRegionsBatch(
        SeedImageRegionsBatchRequest? request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RunImageRegionSeedBatchCommand(request?.BatchSize),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTableRegionCandidates(
        int? minImageRegions,
        int? limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetTableRegionCandidatesQuery(minImageRegions, limit),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> PurgeStale(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PurgeStaleDocumentsCommand(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> CleanupOrphans(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CleanupOrphansCommand(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDistribution(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPdfStatusDistributionQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteKbDocument(
        Guid pdfId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteKbDocumentCommand(pdfId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}

internal record BulkDeletePdfsRequest(List<Guid> PdfIds);
internal record ReindexDocumentRequest(string? IndexerVersion);
internal record SeedImageRegionsRequest(string HiResJson, double? MinAreaFraction = null);
internal record SeedImageRegionsBatchRequest(int? BatchSize = null);
