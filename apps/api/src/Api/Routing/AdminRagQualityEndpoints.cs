using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for RAG quality reporting.
/// Returns index-health statistics: document counts, RAPTOR summaries,
/// entity relations, embedded chunks, top games breakdown, and enhancement statuses.
/// </summary>
internal static class AdminRagQualityEndpoints
{
    public static RouteGroupBuilder MapAdminRagQualityEndpoints(this RouteGroupBuilder group)
    {
        var ragQualityGroup = group.MapGroup("/admin/rag-quality")
            .WithTags("Admin", "RAG Quality")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/rag-quality/report
        ragQualityGroup.MapGet("/report", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRagQualityReportQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetRagQualityReport")
        .WithSummary("Get RAG index quality report with game-level health breakdown")
        .WithDescription("Returns index-health statistics including document counts, RAPTOR summaries, entity relations, embedded chunks, top games by chunk count, and enhancement tier statuses.")
        .Produces(200)
        .Produces(401)
        .Produces(403);

        return group;
    }
}
