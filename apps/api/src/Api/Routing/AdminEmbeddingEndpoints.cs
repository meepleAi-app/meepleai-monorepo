using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for embedding service monitoring.
/// Issue #4878: Embedding Service Dashboard
/// </summary>
internal static class AdminEmbeddingEndpoints
{
    public static RouteGroupBuilder MapAdminEmbeddingEndpoints(this RouteGroupBuilder group)
    {
        var embeddingGroup = group.MapGroup("/admin/embedding")
            .WithTags("Admin", "Embedding")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/embedding/info - Service info + health. #4446: through a query, so the
        // dimension is the API's and not a number written here.
        embeddingGroup.MapGet("/info", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetEmbeddingServiceInfoQuery(), ct).ConfigureAwait(false)));

        // GET /api/v1/admin/embedding/metrics - Prometheus metrics parsed to JSON. #4512: through a
        // query, with the one reader the pipeline health also uses.
        embeddingGroup.MapGet("/metrics", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetEmbeddingServiceMetricsQuery(), ct).ConfigureAwait(false)));

        return group;
    }
}
