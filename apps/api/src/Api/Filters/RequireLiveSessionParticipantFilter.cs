using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

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
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
        => LiveSessionAccessGate.InvokeAsync(context, next, static ctx => ctx.Authorized);
}
