using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Repository interface for the <see cref="GameNightInvitation"/> aggregate.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IGameNightEventRepository"/>: that repo persists
/// the parent event with its by-UserId RSVP collection; this one persists the
/// public, token-addressable invitation aggregate (sent to email addresses
/// possibly not registered yet).
/// </remarks>
internal interface IGameNightInvitationRepository : IRepository<GameNightInvitation, Guid>
{
    /// <summary>
    /// Looks up an invitation by its public token. Returns <c>null</c> when
    /// the token does not match any persisted invitation.
    /// </summary>
    Task<GameNightInvitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an invitation by its unsubscribe key (#4846), whatever the invitation's state: the key sits in an
    /// email that may outlive the invitation. Returns <c>null</c> for a key nobody holds, and for the RSVP token.
    /// </summary>
    Task<GameNightInvitation?> GetByUnsubscribeKeyAsync(string unsubscribeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all invitations belonging to a single game night, regardless of
    /// status. Used by the organizer-facing list and by the
    /// <c>AcceptedSoFar</c> aggregate when reading by token.
    /// </summary>
    Task<IReadOnlyList<GameNightInvitation>> GetByGameNightIdAsync(
        Guid gameNightId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the invitations on a game night that are currently in
    /// <see cref="Domain.Enums.GameNightInvitationStatus.Accepted"/>.
    /// Used to compute <c>AcceptedSoFar</c> efficiently without materializing
    /// the full list.
    /// </summary>
    Task<int> CountAcceptedByGameNightIdAsync(
        Guid gameNightId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True iff a <see cref="Domain.Enums.GameNightInvitationStatus.Pending"/>
    /// invitation already exists for the given <paramref name="gameNightId"/>
    /// and normalized <paramref name="email"/>. Used by the create command to
    /// reject duplicate invites without round-tripping the aggregate.
    /// </summary>
    Task<bool> ExistsPendingByEmailAsync(
        Guid gameNightId, string email, CancellationToken cancellationToken = default);

    // The three «written to» marks (#4744). Each writes ONLY its own column, straight away and outside the unit of
    // work, and only if the mark is still empty. The handlers that set them run seconds after the emails, from a copy
    // of the invitation read before them: rewriting that copy whole (UpdateAsync) would undo an answer or a withdrawal
    // that arrived in between, and with the row version it would be refused and lose the mark, so the next delivery
    // would write the guest again. A mark is a fact about an email, not a change to the aggregate.

    /// <summary>Records that the RSVP confirmation email was written to this invitation's address.</summary>
    Task MarkRsvpConfirmationSentAsync(Guid invitationId, DateTimeOffset sentAt, CancellationToken cancellationToken = default);

    /// <summary>Records that the cancelled-night notice was written to this invitation's address (#4853).</summary>
    Task MarkCancellationNoticeSentAsync(Guid invitationId, DateTimeOffset sentAt, CancellationToken cancellationToken = default);

    /// <summary>Records that the 24 hour reminder was written to this invitation's address (#4846).</summary>
    Task MarkReminder24hSentAsync(Guid invitationId, DateTimeOffset sentAt, CancellationToken cancellationToken = default);
}
