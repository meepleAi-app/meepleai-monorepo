using System.Linq.Expressions;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// The one EF form of «this user accepted this email invitation while logged in» (#4759): the
/// invitation answered by that account, still <c>Accepted</c>, <em>and addressed to that account's
/// email</em>. The last clause is the decision of 2026-11-05: the link is a credential to answer,
/// not to sit at the table, so a link forwarded to somebody else does not make them a participant
/// (they can still answer from it, as before). A withdrawal or a decline changes the status, and
/// the match goes with it. Every query that admits an email guest — the lists and the detail gate
/// in <c>GameNightEventRepository</c>, the calendar conflicts — composes this, so the lists and the
/// detail cannot disagree about who sits at the table.
/// </summary>
internal static class GameNightInvitationQueries
{
    /// <summary>
    /// The address of the account, normalized the way the invitations store theirs
    /// (<see cref="GameNightGuestEmailOptOut.Normalize"/>), or <c>null</c> for an unknown account or
    /// an empty id. A caller with no address matches no invitation.
    /// </summary>
    public static async Task<string?> NormalizedEmailOfAsync(
        MeepleAiDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (userId == Guid.Empty)
            return null;

        var email = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(email) ? null : GameNightGuestEmailOptOut.Normalize(email);
    }

    /// <param name="userId">The account that answered.</param>
    /// <param name="normalizedEmail">That account's address, from <see cref="NormalizedEmailOfAsync"/>.</param>
    public static Expression<Func<GameNightInvitationEntity, bool>> AcceptedBy(Guid userId, string normalizedEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        var accepted = nameof(GameNightInvitationStatus.Accepted);
        return i => i.RespondedByUserId == userId && i.Status == accepted && i.Email == normalizedEmail;
    }

    /// <summary>
    /// #4921: the invitation this account ANSWERED while logged in — accepted or declined — with the same
    /// binding as <see cref="AcceptedBy"/> (answered by that account, addressed to that account's email).
    /// It is the row an email guest can read their RSVP state from, and change from the app: a decline
    /// is still theirs, even if it no longer seats them (<see cref="AcceptedBy"/> is the participation
    /// rule, this is the ownership rule).
    /// </summary>
    public static Expression<Func<GameNightInvitationEntity, bool>> AnsweredBy(Guid userId, string normalizedEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        var accepted = nameof(GameNightInvitationStatus.Accepted);
        var declined = nameof(GameNightInvitationStatus.Declined);
        return i => i.RespondedByUserId == userId
            && i.Email == normalizedEmail
            && (i.Status == accepted || i.Status == declined);
    }

    /// <summary>
    /// #4759: the one EF form of «this user sits at this table». Organizer, an RSVP row, or an email
    /// invitation the user accepted while logged in (no RSVP row is written on that path). Mirrors
    /// <c>GameNightParticipation</c> on the loaded aggregate: change one, change the other. The lists and
    /// the detail gate of <c>GameNightEventRepository</c> compose it; since #4943 so do the statistics
    /// that start from «the nights I was at» (friends, regulars).
    /// </summary>
    public static async Task<Expression<Func<GameNightEventEntity, bool>>> ParticipatedByAsync(
        MeepleAiDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        // The invitation rule is AcceptedBy, composed as a correlated subquery: the same expression
        // HasAcceptedEmailInvitationAsync applies, so the lists and the detail gate cannot drift apart. It
        // needs the account's address (one read), and an account without one is admitted by the
        // organizer/RSVP part only.
        var email = await NormalizedEmailOfAsync(db, userId, cancellationToken).ConfigureAwait(false);
        if (email is null)
            return e => e.OrganizerId == userId || e.Rsvps.Any(r => r.UserId == userId);

        var acceptedByUser = db.GameNightInvitations.Where(AcceptedBy(userId, email));
        return e => e.OrganizerId == userId
            || e.Rsvps.Any(r => r.UserId == userId)
            || acceptedByUser.Any(i => i.GameNightId == e.Id);
    }

    /// <summary>A seat taken through an email invitation by a registered account (#4943).</summary>
    public sealed record AcceptedGuestSeat(Guid GameNightId, Guid UserId, DateTimeOffset? RespondedAt);

    /// <summary>
    /// #4943: the registered accounts that sit at some nights through an email invitation — the
    /// <see cref="AcceptedBy"/> rule read from the other side, for many users at once: invitation
    /// <c>Accepted</c>, answered by an account, <em>and addressed to that account's email</em>. The address
    /// binding is checked here, in memory, after one join with the users: the users' addresses are stored as
    /// typed, the invitations' normalized, and the normalization is C#. A forwarded link does not seat the
    /// account that answered it. <paramref name="scope"/> narrows the invitations (by night, by account).
    /// </summary>
    public static async Task<IReadOnlyList<AcceptedGuestSeat>> AcceptedGuestSeatsAsync(
        MeepleAiDbContext db,
        Expression<Func<GameNightInvitationEntity, bool>> scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scope);

        var accepted = nameof(GameNightInvitationStatus.Accepted);
        var rows = await db.GameNightInvitations
            .AsNoTracking()
            .Where(i => i.Status == accepted && i.RespondedByUserId != null)
            .Where(scope)
            .Join(
                db.Users.AsNoTracking(),
                i => i.RespondedByUserId,
                u => (Guid?)u.Id,
                (i, u) => new { i.GameNightId, UserId = u.Id, UserEmail = u.Email, InvitationEmail = i.Email, i.RespondedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.UserEmail)
                && string.Equals(GameNightGuestEmailOptOut.Normalize(r.UserEmail), r.InvitationEmail, StringComparison.Ordinal))
            .Select(r => new AcceptedGuestSeat(r.GameNightId, r.UserId, r.RespondedAt))
            .ToList();
    }
}
