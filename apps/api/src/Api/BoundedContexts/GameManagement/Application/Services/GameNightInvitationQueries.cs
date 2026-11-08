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
}
