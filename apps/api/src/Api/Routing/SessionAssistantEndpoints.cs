using System.Text.Json;
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

        return group;
    }

    private static async Task HandleAsk(
        [FromRoute] Guid sessionId,
        [FromBody] AskSessionAssistantRequest request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AskSessionAssistantCommand(sessionId, httpContext.User.GetUserId(), request.Question, request.Language);
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

    internal sealed record AskSessionAssistantRequest(string Question, string? Language);
}
