using Api.BoundedContexts.Administration.Application.Queries.GameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.SetGameKbSettings;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameKbSettings;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for per-game KB document management.
/// KB-01: List indexed documents for a game.
/// KB-02: Remove a document from a game's KB.
/// KB-08: Paginated feedback review per-game.
/// KB-10: Get/set per-game KB settings overrides.
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

        // KB-08: GET /api/v1/admin/kb/games/{gameId}/feedback
        g.MapGet("/{gameId:guid}/feedback", async (
            Guid gameId,
            string? outcome,
            DateTime? from,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminKbFeedbackQuery(
                gameId, outcome, from,
                page ?? 1, pageSize ?? 20), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetAdminKbFeedback")
        .WithSummary("Lista feedback utenti su risposte KB per-gioco (admin)");

        // KB-10: GET /api/v1/admin/kb/games/{gameId}/settings
        g.MapGet("/{gameId:guid}/settings", async (Guid gameId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetGameKbSettingsQuery(gameId), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetGameKbSettings")
        .WithSummary("Impostazioni KB override per un gioco (admin)");

        // KB-10: PUT /api/v1/admin/kb/games/{gameId}/settings
        g.MapPut("/{gameId:guid}/settings", async (
            Guid gameId,
            [FromBody] SetGameKbSettingsRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new SetGameKbSettingsCommand(
                gameId, req.MaxChunks, req.ChunkSize, req.CacheEnabled, req.Language), ct)
                .ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("SetGameKbSettings")
        .WithSummary("Imposta override impostazioni KB per-gioco (admin)");

        return group;
    }
}

internal sealed record SetGameKbSettingsRequest(
    int? MaxChunks, int? ChunkSize, bool? CacheEnabled, string? Language);
