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

        var cutoff = DateTimeOffset.UtcNow.Subtract(RegularsWindow);

        // Pull the raw RSVPs in the window scoped to the organizer's game nights,
        // joined with the User projection needed for the DTO. Grouping is done in
        // memory (post-ToListAsync) because EF Core InMemory does not support
        // composite-key GroupBy → Select translation, and Postgres group cardinality
        // is bounded by the number of distinct invitees (≤ 49 × N events) which
        // remains small in practice.
        var raw = await _context.GameNightRsvps
            .AsNoTracking()
            .Where(r => r.CreatedAt > cutoff)
            .Join(
                _context.GameNightEvents.AsNoTracking().Where(gn => gn.OrganizerId == query.UserId),
                rsvp => rsvp.EventId,
                gn => gn.Id,
                (rsvp, _) => rsvp)
            .Join(
                _context.Users.AsNoTracking(),
                rsvp => rsvp.UserId,
                user => user.Id,
                (rsvp, user) => new
                {
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    rsvp.CreatedAt,
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
                LastInvitedAt: group.Max(x => x.CreatedAt)))
            .OrderByDescending(dto => dto.EventCount)
            .ThenByDescending(dto => dto.LastInvitedAt)
            .Take(query.Limit)
            .ToList();
    }
}
