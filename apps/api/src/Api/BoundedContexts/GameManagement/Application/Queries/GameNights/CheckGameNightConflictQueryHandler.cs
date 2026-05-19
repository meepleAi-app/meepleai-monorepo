using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handler for <see cref="CheckGameNightConflictQuery"/>.
/// Issue #950 (W1-PR2): returns events within ±2h of <c>ProposedAt</c> that the
/// current user is involved in (organizer or invitee). Terminal-status events
/// (Cancelled, Completed) are excluded. Spec §7b.3 + §12b BE-4.
/// </summary>
internal sealed class CheckGameNightConflictQueryHandler
    : IQueryHandler<CheckGameNightConflictQuery, ConflictCheckDto>
{
    /// <summary>
    /// Spec §7b.3: ±2 hours around the proposed time.
    /// </summary>
    private static readonly TimeSpan WindowRadius = TimeSpan.FromHours(2);

    /// <summary>
    /// Active statuses that compete for the user's calendar. Mirrors
    /// <c>GameNightStatus.{Draft, Published, InProgress}</c>. Cancelled and
    /// Completed are excluded; the wizard should warn only about commitments
    /// still in flight.
    /// </summary>
    private static readonly string[] ActiveStatuses = { "Draft", "Published", "InProgress" };

    private const string OrganizerRole = "organizer";
    private const string InviteeRole = "invitee";

    private readonly MeepleAiDbContext _context;

    public CheckGameNightConflictQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ConflictCheckDto> Handle(
        CheckGameNightConflictQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var windowStart = query.ProposedAt.Subtract(WindowRadius);
        var windowEnd = query.ProposedAt.Add(WindowRadius);

        // Pull active candidate events in the window organized by the user.
        var organizerHits = await _context.GameNightEvents
            .AsNoTracking()
            .Where(gn => gn.OrganizerId == query.UserId
                && ActiveStatuses.Contains(gn.Status)
                && gn.ScheduledAt >= windowStart
                && gn.ScheduledAt <= windowEnd)
            .Select(gn => new { gn.Id, gn.Title, gn.ScheduledAt, Role = OrganizerRole })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Pull active candidate events in the window the user is invited to but
        // does NOT organize (those are already in `organizerHits`).
        var inviteeHits = await _context.GameNightRsvps
            .AsNoTracking()
            .Where(r => r.UserId == query.UserId)
            .Join(
                _context.GameNightEvents.AsNoTracking()
                    .Where(gn => gn.OrganizerId != query.UserId
                        && ActiveStatuses.Contains(gn.Status)
                        && gn.ScheduledAt >= windowStart
                        && gn.ScheduledAt <= windowEnd),
                rsvp => rsvp.EventId,
                gn => gn.Id,
                (_, gn) => new { gn.Id, gn.Title, gn.ScheduledAt, Role = InviteeRole })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var conflicts = organizerHits
            .Concat(inviteeHits)
            .Select(x => new ConflictEntryDto(x.Id, x.Title, x.ScheduledAt, x.Role))
            .OrderBy(c => c.ScheduledAt)
            .ToList();

        return new ConflictCheckDto(
            HasConflict: conflicts.Count > 0,
            Conflicts: conflicts);
    }
}
