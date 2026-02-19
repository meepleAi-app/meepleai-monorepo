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

        // Phase 4: Bulk operations
        group.MapPost("/bulk/delete", BulkDelete)
            .WithName("BulkDeletePdfs")
            .WithSummary("Bulk delete PDF documents");

        // Phase 5: Maintenance operations
        group.MapPost("/{pdfId:guid}/reindex", ReindexDocument)
            .WithName("ReindexDocument")
            .WithSummary("Reindex a PDF document (delete chunks, reset to Pending)");

        group.MapPost("/maintenance/purge-stale", PurgeStale)
            .WithName("PurgeStaleDocuments")
            .WithSummary("Mark documents stuck in processing (>24h) as failed");

        group.MapPost("/maintenance/cleanup-orphans", CleanupOrphans)
            .WithName("CleanupOrphans")
            .WithSummary("Delete orphaned text chunks referencing non-existent PDFs");

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
        var command = new BulkDeletePdfsCommand(
            request.PdfIds.Select(id => Guid.Parse(id)).ToList()
        );
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReindexDocument(
        Guid pdfId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ReindexDocumentCommand(pdfId), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(new { success = true, message = "Document queued for reindexing" });
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
}

internal record BulkDeletePdfsRequest(List<string> PdfIds);
