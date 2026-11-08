using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles retrieval of upcoming published game nights.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Enriches DTO with OrganizerName via batch user lookup.
/// </summary>
internal sealed class GetUpcomingGameNightsQueryHandler : IQueryHandler<GetUpcomingGameNightsQuery, IReadOnlyList<GameNightDto>>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetUpcomingGameNightsQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GameNightDto>> Handle(GetUpcomingGameNightsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = await _repository.GetUpcomingAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        var organizerIds = events.Select(e => e.OrganizerId).Distinct().ToList();
        var organizerNames = await GetUserDisplayNamesAsync(organizerIds, cancellationToken).ConfigureAwait(false);

        // #4706: guests invited by email are not in the RSVP rows; add them to the counts.
        var nightIds = events.Select(e => e.Id).ToList();
        var guests = await GuestInvitationCounts.LoadAsync(_context, nightIds, cancellationToken).ConfigureAwait(false);

        // #4921: the nights the viewer sits at as an email guest are in this list (the repository admits
        // them) but have no RSVP row for them; their state comes from the invitation, asked once for all.
        var guestStatuses = await _repository
            .GetEmailGuestRsvpStatusesAsync(nightIds, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return events.Select(e => GameNightMapperHelper.MapToDto(
            e, organizerNames.GetValueOrDefault(e.OrganizerId, "Unknown"), query.UserId,
            guests.GetValueOrDefault(e.Id),
            GameNightParticipation.ViewerRsvpStatus(e, query.UserId, guestStatuses))).ToList();
    }

    private async Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(u => u.Id, u => u.DisplayName ?? u.Email ?? "Unknown");
    }
}
