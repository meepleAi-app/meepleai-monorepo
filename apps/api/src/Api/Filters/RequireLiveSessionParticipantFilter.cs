using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Filters;

/// <summary>
/// Endpoint filter enforcing per-session participant authorization on live-session endpoints (#2573).
/// The caller must be the session creator or an active linked player.
/// Returns 401 if unauthenticated, 404 if the session does not exist (or the route lacks a parsable
/// {sessionId}), 403 if authenticated but not a participant. Reads the {sessionId} route value.
///
/// Apply via .RequireLiveSessionParticipant() AFTER .RequireAuthenticatedUser().
/// Mirrors the response convention of RequireAuthenticatedUser/RequireAdminSession (returns Results.*),
/// delegating the authz decision to <see cref="GetLiveSessionParticipantContextQuery"/>.
/// </summary>
internal sealed class RequireLiveSessionParticipantFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
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

        if (!ctx.Authorized)
        {
            return Results.StatusCode(403);
        }

        return await next(context).ConfigureAwait(false);
    }
}
