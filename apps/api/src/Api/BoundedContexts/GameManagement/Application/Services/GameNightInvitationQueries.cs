using System.Linq.Expressions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure.Entities.GameManagement;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// The one EF form of «this user accepted this email invitation while logged in» (#4759): the
/// invitation answered by that account and still <c>Accepted</c>. A withdrawal or a decline changes
/// the status, and the match goes with it. Every query that admits an email guest — the lists and
/// the detail gate in <c>GameNightEventRepository</c>, the calendar conflicts — composes this, so
/// the lists and the detail cannot disagree about who sits at the table.
/// </summary>
internal static class GameNightInvitationQueries
{
    public static Expression<Func<GameNightInvitationEntity, bool>> AcceptedBy(Guid userId)
    {
        var accepted = nameof(GameNightInvitationStatus.Accepted);
        return i => i.RespondedByUserId == userId && i.Status == accepted;
    }
}
