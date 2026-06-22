using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
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
/// </summary>
internal sealed class GetRegularsQueryHandler : IQueryHandler<GetRegularsQuery, IReadOnlyList<RegularDto>>
{
    /// <summary>
    /// Spec §7b.2: regular invitations within the last 12 months. Older RSVPs are
    /// excluded so the suggestion list reflects current play patterns, not legacy data.
    /// </summary>
    private static readonly TimeSpan RegularsWindow = TimeSpan.FromDays(365);

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
        //
        // Grouping is done in memory (post-ToListAsync) because EF Core InMemory
        // does not support composite-key GroupBy → Select translation, and Postgres
        // group cardinality is bounded by the number of distinct invitees per
        // organizer in the window (small in practice).
        var raw = await _context.GameNightRsvps
            .AsNoTracking()
            .Where(rsvp => rsvp.Status != "Declined")
            .Where(rsvp => rsvp.UserId != query.UserId)
            .Join(
                _context.GameNightEvents.AsNoTracking()
                    .Where(gn => gn.OrganizerId == query.UserId
                        && gn.ScheduledAt > cutoff
                        && gn.ScheduledAt <= now),
                rsvp => rsvp.EventId,
                gn => gn.Id,
                (rsvp, gn) => new { rsvp.UserId, gn.ScheduledAt })
            .Join(
                _context.Users.AsNoTracking(),
                pair => pair.UserId,
                user => user.Id,
                (pair, user) => new
                {
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    pair.ScheduledAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return raw
            .GroupBy(x => new { x.Id, x.DisplayName, x.Email })
            .Select(group => new RegularDto(
                Id: group.Key.Id,
                DisplayName: group.Key.DisplayName ?? group.Key.Email,
                Email: group.Key.Email,
                EventCount: group.Count(),
                LastInvitedAt: group.Max(x => x.ScheduledAt)))
            .OrderByDescending(dto => dto.EventCount)
            .ThenByDescending(dto => dto.LastInvitedAt)
            .Take(query.Limit)
            .ToList();
    }
}
