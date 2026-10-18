using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Panel;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.Extensions;
using Api.Infrastructure.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Spec 2026-10-11 §6.2: l'assistente in partita. Il filtro degli endpoint live controlla la
/// partecipazione; che chi chiede sia il proprietario lo controllano gli handler.
/// </summary>
internal static class SessionAssistantEndpoints
{
    public static RouteGroupBuilder MapSessionAssistantEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/live-sessions/{sessionId}/assistant", HandleGet)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<SessionAssistantPanelDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithTags("LiveSessions")
            .WithSummary("Read the in-game assistant panel")
            .WithDescription("Host only (#4372). Whether the assistant answers (and, if not, the code the turn would return), the conversation and the proposals of the session, for the panel to load and reload.");

        group.MapPost("/live-sessions/{sessionId}/assistant", HandleAsk)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(200, contentType: "text/event-stream")
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // assistant_disabled · assistant_model_without_tools · session_state_invalid
            .WithTags("LiveSessions")
            .WithSummary("Ask the in-game assistant (SSE)")
            .WithDescription("Host only. Streams status, proposal, answer and done (or error) events. The assistant never writes the game: it creates proposals the host confirms.");

        group.MapPost("/live-sessions/{sessionId}/assistant/proposals/{proposalId}/confirm", HandleConfirm)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<AssistantProposalDto>(200)
            .Produces(400)  // una correzione su una proposta di turno
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // proposal_stale · proposal_not_pending · session_state_invalid · proposal_not_applicable
            .Produces(422)  // una correzione fuori dai limiti
            .WithTags("LiveSessions")
            .WithSummary("Confirm a proposal of the in-game assistant")
            .WithDescription("Host only. Applies the proposal to the game: the only write path of the assistant. A second tap returns the current state.");

        group.MapPost("/live-sessions/{sessionId}/assistant/proposals/{proposalId}/reject", HandleReject)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<AssistantProposalDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // proposal_not_pending
            .WithTags("LiveSessions")
            .WithSummary("Reject a proposal of the in-game assistant");

        return group;
    }

    private static async Task<IResult> HandleGet(
        [FromRoute] Guid sessionId,
        [FromQuery] string? language,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var panel = await mediator.Send(
            new GetSessionAssistantQuery(sessionId, httpContext.User.GetUserId(), language), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(panel);
    }

    private static async Task HandleAsk(
        [FromRoute] Guid sessionId,
        [FromBody] AskSessionAssistantRequest request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // #4411: il ruolo decide l'accesso alla KB del gioco in cerca_regole (bypass admin), come in StreamQa.
        var command = new AskSessionAssistantCommand(
            LiveSessionId: sessionId,
            UserId: httpContext.User.GetUserId(),
            UserRole: httpContext.User.FindFirst(ClaimTypes.Role)?.Value,
            Question: request.Question,
            Language: request.Language);
        var events = mediator.CreateStream(command, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var disposal = events.ConfigureAwait(false);

        // Il primo evento arriva dopo i controlli del turno. Se falliscono, l'eccezione esce da qui prima
        // che la risposta sia partita, e il middleware la traduce in 403, 404 o 409 con il suo codice.
        if (!await events.MoveNextAsync().ConfigureAwait(false))
        {
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        do
        {
            await httpContext.Response.WriteAsync(
                $"data: {JsonSerializer.Serialize(events.Current, SseJsonOptions.Default)}\n\n", cancellationToken)
                .ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await events.MoveNextAsync().ConfigureAwait(false));
    }

    private static async Task<IResult> HandleConfirm(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid proposalId,
        [FromBody] ConfirmAssistantProposalRequest? request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var proposal = await mediator.Send(
            new ConfirmAssistantProposalCommand(sessionId, proposalId, httpContext.User.GetUserId(), request?.Points),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(proposal);
    }

    private static async Task<IResult> HandleReject(
        [FromRoute] Guid sessionId, [FromRoute] Guid proposalId, HttpContext httpContext, IMediator mediator, CancellationToken cancellationToken)
    {
        var proposal = await mediator.Send(
            new RejectAssistantProposalCommand(sessionId, proposalId, httpContext.User.GetUserId()), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(proposal);
    }

    /// <summary>Il corpo è opzionale: senza, si conferma l'incremento proposto.</summary>
    internal sealed record ConfirmAssistantProposalRequest(int? Points);

    internal sealed record AskSessionAssistantRequest(string Question, string? Language);
}
