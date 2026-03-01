using Api.BoundedContexts.Administration.Application.Commands;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for storage migration (local → S3).
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
}
