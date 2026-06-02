using Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for indexer pipeline metadata.
/// Issue #1673: registry per dropdown versione reindex.
/// </summary>
internal static class AdminIndexerEndpoints
{
    public static void MapAdminIndexerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/indexer")
            .WithTags("Admin - Indexer")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/versions", GetVersions)
            .WithName("GetIndexerVersions")
            .WithSummary("Returns selectable indexer versions for the reindex dropdown");
    }

    private static async Task<IResult> GetVersions(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var versions = await mediator.Send(new GetIndexerVersionRegistryQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(versions);
    }
}
