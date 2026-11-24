using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

namespace Api.Filters;

/// <summary>
/// Endpoint filter for the live-session commands that only the session creator may send (#4763): pause,
/// resume, complete, save and the removal of a player. The control of a live game is its creator's
/// (<c>CreatedByUserId</c>), as for start and for adding players (#4398); being a linked player, whatever
/// the seat or role (Host included), is not enough. A participant who is not the creator gets the same 403
/// as a stranger, and a missing session answers 404 before 403.
///
/// Apply via .RequireLiveSessionCreator() AFTER .RequireAuthenticatedUser(), in place of
/// .RequireLiveSessionParticipant(): the creator is always a participant.
/// </summary>
internal sealed class RequireLiveSessionCreatorFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
        => LiveSessionAccessGate.InvokeAsync(context, next, static ctx => ctx.IsCreator);
}
