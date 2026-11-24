using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Filters;

/// <summary>
/// The shape shared by the live-session endpoint filters: 401 if unauthenticated, 404 if the {sessionId}
/// route value is missing, unparsable or names no session, 403 if the filter's predicate refuses the
/// caller, else next(). The decision comes from <see cref="GetLiveSessionParticipantContextQuery"/>, so
/// every filter reads the session once and answers 404 before 403.
/// </summary>
internal static class LiveSessionAccessGate
{
    public static async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        Func<LiveSessionParticipantContextResult, bool> allows)
    {
        var httpContext = context.HttpContext;
        var userId = httpContext.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (!httpContext.Request.RouteValues.TryGetValue("sessionId", out var rawSessionId)
            || !Guid.TryParse(rawSessionId?.ToString(), out var sessionId))
        {
            return Results.NotFound(new { error = "Live session not found" });
        }

        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        var ctx = await mediator
            .Send(new GetLiveSessionParticipantContextQuery(sessionId, userId), httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!ctx.Found)
        {
            return Results.NotFound(new { error = "Live session not found" });
        }

        if (!allows(ctx))
        {
            return Results.StatusCode(403);
        }

        return await next(context).ConfigureAwait(false);
    }
}
