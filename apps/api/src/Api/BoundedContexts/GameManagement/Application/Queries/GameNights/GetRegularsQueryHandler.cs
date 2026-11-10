using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handler for <see cref="GetRegularsQuery"/>.
/// Issue #950 (W1-PR2): aggregates registered users the current organizer has
/// invited across past <see cref="Api.Infrastructure.Entities.GameManagement.GameNightEventEntity"/>s
/// in the last 12 months. Ordered by event count DESC, then last invitation DESC.
/// Spec §7b.2 + §12b BE-6.
/// <para>
/// #4943: a registered user who accepted an email invitation while logged in (#4759) has no RSVP row,
/// so they never became a regular. They count as an accepted RSVP does, with the same binding as the
/// lists and the detail gate (<see cref="GameNightInvitationQueries.AcceptedGuestSeatsAsync"/>); a night
/// where the same person has both an RSVP row and an accepted invitation counts once.
/// </para>
/// </summary>
internal sealed class GetRegularsQueryHandler : IQueryHandler<GetRegularsQuery, IReadOnlyList<RegularDto>>
{
    /// <summary>
    /// Spec §7b.2: regular invitations within the last 12 months. Older RSVPs are
    /// excluded so the suggestion list reflects current play patterns, not legacy data.
    /// </summary>
    private static readonly TimeSpan RegularsWindow = TimeSpan.FromDays(365);

    private const string DeclinedRsvp = "Declined";

    private readonly MeepleAiDbContext _context;

    public GetRegularsQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<RegularDto>> Handle(GetRegularsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Subtract(RegularsWindow);

        // PR #1294 review fixes:
        //   - Window predicate uses GameNight.ScheduledAt (when the event happened)
        //     instead of GameNightRsvp.CreatedAt (when the invitation was issued).
        //     The spec describes "past co-participants in the last 12 months", which
        //     is an event-occurrence semantic, not an invitation-creation one.
        //   - Future-scheduled events are excluded so "current play patterns" don't
        //     surface invitees from events that haven't happened yet.
        //   - Declined RSVPs are excluded: a string of declines is the opposite of
        //     a regular play pattern.
        //   - The organizer themselves is excluded from their own regulars list
        //     even if seed/test data has them in the RSVP table.
        var nights = await _context.GameNightEvents
            .AsNoTracking()
            .Where(gn => gn.OrganizerId == query.UserId
                && gn.ScheduledAt > cutoff
                && gn.ScheduledAt <= now)
            .Select(gn => new { gn.Id, gn.ScheduledAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (nights.Count == 0)
        {
            return Array.Empty<RegularDto>();
        }

        var nightIds = nights.Select(n => n.Id).ToList();
        var scheduledAtByNight = nights.ToDictionary(n => n.Id, n => n.ScheduledAt);

        var rsvpSeats = await _context.GameNightRsvps
            .AsNoTracking()
            .Where(rsvp => nightIds.Contains(rsvp.EventId)
                && rsvp.Status != DeclinedRsvp
                && rsvp.UserId != query.UserId)
            .Select(rsvp => new { rsvp.UserId, NightId = rsvp.EventId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // #4943: the seats taken through an email invitation, bound to the account's own address.
        var guestSeats = await GameNightInvitationQueries
            .AcceptedGuestSeatsAsync(_context, i => nightIds.Contains(i.GameNightId), cancellationToken)
            .ConfigureAwait(false);

        // One seat per (person, night): an RSVP row AND an accepted invitation on the same night is one night together.
        var seats = rsvpSeats.Select(s => (s.UserId, s.NightId))
            .Concat(guestSeats.Where(g => g.UserId != query.UserId).Select(g => (g.UserId, NightId: g.GameNightId)))
            .Distinct()
            .ToList();

        if (seats.Count == 0)
        {
            return Array.Empty<RegularDto>();
        }

        var userIds = seats.Select(s => s.UserId).Distinct().ToList();
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userById = users.ToDictionary(u => u.Id);

        // Grouping is done in memory because EF Core InMemory does not support composite-key GroupBy → Select
        // translation, and the cardinality is bounded by the organizer's nights in the window (small in practice).
        return seats
            .Where(s => userById.ContainsKey(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(group =>
            {
                var user = userById[group.Key];
                return new RegularDto(
                    Id: user.Id,
                    DisplayName: user.DisplayName ?? user.Email,
                    Email: user.Email,
                    EventCount: group.Count(),
                    LastInvitedAt: group.Max(s => scheduledAtByNight[s.NightId]));
            })
            .OrderByDescending(dto => dto.EventCount)
            .ThenByDescending(dto => dto.LastInvitedAt)
            .Take(query.Limit)
            .ToList();
    }
}
