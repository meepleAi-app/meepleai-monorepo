using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Who sits at a game night's table, written once (#4759). Before this the rule
/// «organizer or an RSVP row» was copied into every handler that reads a night, and a
/// registered user who accepted an <em>email</em> invitation while logged in matched none of
/// them: no RSVP row is written on that path, so the night answered 403 on every read and
/// stayed out of their lists. They are a participant while the invitation is
/// <c>Accepted</c>: a withdrawal or a decline takes it away again, with no extra step.
/// </summary>
/// <remarks>
/// The organizer and the RSVP members are known from the aggregate already loaded, so they
/// are answered without a roundtrip; only an unknown caller asks the repository about the
/// invitations. The same rule, in its EF form, filters the lists in
/// <c>GameNightEventRepository</c>. An empty caller id is nobody (an endpoint that forgot to
/// derive it from the principal), never a match.
/// </remarks>
internal static class GameNightParticipation
{
    /// <summary>The part of the rule the aggregate can answer by itself.</summary>
    public static bool IsOrganizerOrMember(GameNightEvent night, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(night);

        return userId != Guid.Empty
            && (night.OrganizerId == userId || night.Rsvps.Any(r => r.UserId == userId));
    }

    /// <summary>The whole rule: organizer, RSVP member, or accepted email guest.</summary>
    public static async Task<bool> IsParticipantAsync(
        GameNightEvent night,
        Guid userId,
        IGameNightEventRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (IsOrganizerOrMember(night, userId))
            return true;

        if (userId == Guid.Empty)
            return false;

        return await repository
            .HasAcceptedEmailInvitationAsync(night.Id, userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// #4921: the RSVP state the viewer reads as. A member has a row and that is the answer; an email
    /// guest has none, and reads as the invitation they answered while logged in (<c>Accepted</c> or
    /// <c>Declined</c>), derived by the repository; the organizer and a stranger read as <c>null</c>, as
    /// before. The reads derive, nothing is written: the invitation stays the one record of the guest,
    /// so the counts (#4706) keep summing it once.
    /// </summary>
    public static async Task<RsvpStatus?> ViewerRsvpStatusAsync(
        GameNightEvent night,
        Guid viewerId,
        IGameNightEventRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(night);
        ArgumentNullException.ThrowIfNull(repository);

        // The organizer has no RSVP and never had one: no roundtrip for them.
        if (viewerId == Guid.Empty || night.OrganizerId == viewerId)
            return null;

        var rsvp = night.GetRsvp(viewerId);
        if (rsvp is not null)
            return rsvp.Status;

        var derived = await repository
            .GetEmailGuestRsvpStatusesAsync([night.Id], viewerId, cancellationToken)
            .ConfigureAwait(false);
        return derived.TryGetValue(night.Id, out var status) ? status : null;
    }
}
