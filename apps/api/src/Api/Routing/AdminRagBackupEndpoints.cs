using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Api.BoundedContexts.Administration.Application.Queries.RagBackup;
using Api.Filters;
using MediatR;

namespace Api.Routing;

internal static class AdminRagBackupEndpoints
{
    public static void MapAdminRagBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/rag-backup")
            .WithTags("Admin - RAG Backup")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /admin/rag-backup/export?dryRun=false&gameId=
        group.MapPost("/export", ExportRagData)
            .WithName("ExportRagData")
            .WithSummary("Export all processed RAG data to backup storage");

        // POST /admin/rag-backup/import?snapshotPath=&reEmbed=false
        group.MapPost("/import", ImportRagData)
            .WithName("ImportRagData")
            .WithSummary("Import RAG data from a backup snapshot");

        // GET /admin/rag-backup/snapshots
        group.MapGet("/snapshots", ListSnapshots)
            .WithName("ListRagSnapshots")
            .WithSummary("List available RAG backup snapshots");

        // GET /admin/rag-backup/snapshots/{id}
        group.MapGet("/snapshots/{id}", GetSnapshot)
            .WithName("GetRagSnapshot")
            .WithSummary("Get download URL for a specific snapshot");
    }

    private static async Task<IResult> ExportRagData(
        bool dryRun = false,
        Guid? gameId = null,
        IMediator mediator = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ExportRagDataCommand
        {
            DryRun = dryRun,
            GameIdFilter = gameId?.ToString(),
        }, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> ImportRagData(
        string snapshotPath = "",
        bool reEmbed = false,
        IMediator mediator = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ImportRagDataCommand
        {
            SnapshotPath = snapshotPath,
            ReEmbed = reEmbed
        }, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> ListSnapshots(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new DownloadRagSnapshotQuery(), cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSnapshot(
        string id,
        string? gameSlug = null,
        IMediator mediator = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new DownloadRagSnapshotQuery(SnapshotId: id, GameSlug: gameSlug),
            cancellationToken).ConfigureAwait(false);

        if (result.Error != null)
            return Results.NotFound(result);

        return Results.Ok(result);
    }
}
