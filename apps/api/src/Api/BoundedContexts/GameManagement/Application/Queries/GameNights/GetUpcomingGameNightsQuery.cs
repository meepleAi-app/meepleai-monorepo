using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get upcoming published game nights.
/// Issue #46: GameNight API endpoints.
/// </summary>
/// <param name="CallerUserId">
/// #2978 (invariante #17): the authenticated viewer, used to resolve their own RSVP status
/// (<c>MyRsvpStatus</c>) on each returned DTO for the dashboard pending-invitee card.
/// </param>
internal record GetUpcomingGameNightsQuery(Guid CallerUserId) : IQuery<IReadOnlyList<GameNightDto>>;
