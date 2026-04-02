using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing endpoints for per-game Knowledge Base operations.
/// KB-06: POST feedback on a KB chat response.
/// </summary>
internal static class UserGameKbEndpoints
{
    public static RouteGroupBuilder MapUserGameKbEndpoints(this RouteGroupBuilder group)
    {
        // KB-06: feedback utente su risposta chat
        group.MapPost("/games/{gameId:guid}/knowledge-base/feedback", async (
            Guid gameId,
            [FromBody] KbFeedbackRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = httpContext.TryGetActiveSession();
            if (!authenticated) return error!;

            await mediator.Send(new SubmitKbFeedbackCommand(
                session.User!.Id,
                gameId,
                request.ChatSessionId,
                request.MessageId,
                request.Outcome,
                request.Comment), ct).ConfigureAwait(false);

            return Results.NoContent();
        })
        .RequireSession()
        .WithName("SubmitKbFeedback")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Invia feedback utente su una risposta chat KB");

        return group;
    }

    private sealed record KbFeedbackRequest(
        Guid ChatSessionId,
        Guid MessageId,
        string Outcome,
        string? Comment);
}
