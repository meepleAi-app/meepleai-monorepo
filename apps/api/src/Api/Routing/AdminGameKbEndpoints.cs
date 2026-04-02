using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for per-game KB document management.
/// KB-01: List indexed documents for a game.
/// KB-02: Remove a document from a game's KB.
/// </summary>
internal static class AdminGameKbEndpoints
{
    public static RouteGroupBuilder MapAdminGameKbEndpoints(this RouteGroupBuilder group)
    {
        var g = group.MapGroup("/admin/kb/games")
            .WithTags("Admin", "KnowledgeBase")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // KB-01: GET /api/v1/admin/kb/games/{gameId}/documents
        g.MapGet("/{gameId:guid}/documents", async (
            Guid gameId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminGameKbDocumentsQuery(gameId), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetAdminGameKbDocuments")
        .WithSummary("Lista documenti KB indicizzati per un gioco (admin)");

        // KB-02: DELETE /api/v1/admin/kb/games/{gameId}/documents/{vectorDocId}
        g.MapDelete("/{gameId:guid}/documents/{vectorDocId:guid}", async (
            Guid gameId,
            Guid vectorDocId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RemoveDocumentFromKbCommand(vectorDocId, gameId), ct)
                .ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("RemoveDocumentFromKb")
        .WithSummary("Rimuove un documento dalla KB di un gioco (admin)");

        return group;
    }
}
