using Api.BoundedContexts.Administration.Application.Queries.GameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for per-game KB document management.
/// KB-01: List indexed documents for a game.
/// KB-02: Remove a document from a game's KB.
/// KB-08: Paginated feedback review; #4469 per profile version, without a game UUID.
/// #4169: the per-game KB settings override (KB-10) is gone: it was saved, never read by the
/// answer path. Retrieval settings belong to the agent profile (ADR-095).
/// KB-GAMES: Overview dashboard with KB status per game.
/// </summary>
internal static class AdminGameKbEndpoints
{
    public static RouteGroupBuilder MapAdminGameKbEndpoints(this RouteGroupBuilder group)
    {
        var g = group.MapGroup("/admin/kb/games")
            .WithTags("Admin", "KnowledgeBase")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // KB-GAMES: GET /api/v1/admin/kb/games — overview di tutti i giochi con stato KB
        g.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetGameKbStatusesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetGameKbStatuses")
        .WithSummary("Overview KB status per tutti i giochi (admin)");

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

        // KB-08, #4469: i feedback si leggono senza l'UUID del gioco, per versione del profilo. La rotta per
        // gioco GET /admin/kb/games/{gameId}/feedback è ritirata (EndpointContractTests.RetiredRoutes).
        var feedback = group.MapGroup("/admin/kb/feedback")
            .WithTags("Admin", "KnowledgeBase")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/kb/feedback?version={id|none}&gameId=&outcome=&page=&pageSize=
        feedback.MapGet("/", async (
            Guid? gameId,
            string? version,
            string? outcome,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminKbFeedbackQuery(
                gameId, version, outcome,
                page ?? 1, pageSize ?? 20), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetAdminKbFeedback")
        .WithSummary("Feedback utenti sulle risposte KB, per versione del profilo e per gioco (admin)");

        // GET /api/v1/admin/kb/feedback/summary
        feedback.MapGet("/summary", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminKbFeedbackSummaryQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetAdminKbFeedbackSummary")
        .WithSummary("Feedback utili e non utili per versione del profilo, e i giochi con feedback (admin)");

        return group;
    }
}
