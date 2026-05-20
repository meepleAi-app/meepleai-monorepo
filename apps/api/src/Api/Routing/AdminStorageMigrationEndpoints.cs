using Api.BoundedContexts.Administration.Application.Commands;
using Api.Filters;
using Api.Services.Pdf;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for storage migration (local → S3, and #1314 PR 2 layout migration).
/// </summary>
internal static class AdminStorageMigrationEndpoints
{
    public static void MapAdminStorageMigrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/storage")
            .WithTags("Admin - Storage Migration")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/migrate", MigrateStorage)
            .WithName("MigrateStorage")
            .WithSummary("Migrate files from local filesystem to S3 storage")
            .WithDescription("Scans pdf_uploads/ directory and uploads files to S3. " +
                "Use dryRun=true to preview without executing. Idempotent: skips files already on S3.");

        // Issue #1333: storage-layout migration Phase 1 enqueue endpoint.
        group.MapPost("/migration/enqueue", EnqueueStorageMigration)
            .WithName("EnqueueStorageMigration")
            .WithSummary("Enqueue legacy storage objects for outbox-driven migration")
            .WithDescription("Enumerates S3 objects under legacyPrefix and inserts " +
                "storage_operation_outbox rows for the StorageOperationOutboxBackgroundService " +
                "drainer to move them to the new categorized layout. " +
                "Idempotent: duplicate LegacyKey insertions are deduped (Skipped count). " +
                "Use dryRun=true to preview enumeration without inserting rows.");

        // Issue #1333: storage-layout migration Phase B backout endpoint.
        group.MapPost("/migration/reverse", ReverseStorageMigration)
            .WithName("ReverseStorageMigration")
            .WithSummary("Reverse an in-flight or completed storage migration (Phase B backout)")
            .WithDescription("Reverses Sent rows (NewKey → LegacyKey + delete NewKey) and " +
                "transitions Pending rows to Reverted state. Idempotent: rows already in " +
                "FailedPermanent or Reverted are skipped.");
    }

    private static async Task<IResult> MigrateStorage(
        bool dryRun,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MigrateStorageCommand(dryRun), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> EnqueueStorageMigration(
        EnqueueStorageMigrationRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new EnqueueStorageMigrationCommand(
            MigrationId: request.MigrationId,
            LegacyPrefix: request.LegacyPrefix,
            Category: request.Category,
            DryRun: request.DryRun), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReverseStorageMigration(
        ReverseStorageMigrationRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReverseStorageMigrationCommand(
            MigrationId: request.MigrationId,
            DryRun: request.DryRun), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for <c>POST /admin/storage/migration/enqueue</c>.
/// </summary>
internal sealed record EnqueueStorageMigrationRequest(
    Guid MigrationId,
    string LegacyPrefix,
    BlobCategory Category,
    bool DryRun = false);

/// <summary>
/// Request body for <c>POST /admin/storage/migration/reverse</c>.
/// </summary>
internal sealed record ReverseStorageMigrationRequest(
    Guid MigrationId,
    bool DryRun = false);
