using System.Runtime.InteropServices;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// How many guests invited by email a game night has, split the way the member RSVPs are.
/// #4706: the invitations live in <c>game_night_invitations</c>, apart from the RSVP rows, so the
/// accepted/pending/invited counts read from the aggregate alone ignored them. Cancelled
/// invitations are not guests any more and do not count; a declined or expired one still counts as
/// invited, as a declined member RSVP does.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GuestInvitationCounts(int Accepted, int Pending, int Total)
{
    public static async Task<IReadOnlyDictionary<Guid, GuestInvitationCounts>> LoadAsync(
        MeepleAiDbContext context,
        IReadOnlyCollection<Guid> gameNightIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (gameNightIds.Count == 0)
            return new Dictionary<Guid, GuestInvitationCounts>();

        var cancelled = nameof(GameNightInvitationStatus.Cancelled);
        var rows = await context.GameNightInvitations
            .AsNoTracking()
            .Where(i => gameNightIds.Contains(i.GameNightId) && i.Status != cancelled)
            .GroupBy(i => new { i.GameNightId, i.Status })
            .Select(g => new { g.Key.GameNightId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var accepted = nameof(GameNightInvitationStatus.Accepted);
        var pending = nameof(GameNightInvitationStatus.Pending);

        return rows
            .GroupBy(r => r.GameNightId)
            .ToDictionary(
                g => g.Key,
                g => new GuestInvitationCounts(
                    Accepted: g.Where(r => string.Equals(r.Status, accepted, StringComparison.Ordinal)).Sum(r => r.Count),
                    Pending: g.Where(r => string.Equals(r.Status, pending, StringComparison.Ordinal)).Sum(r => r.Count),
                    Total: g.Sum(r => r.Count)));
    }
}
