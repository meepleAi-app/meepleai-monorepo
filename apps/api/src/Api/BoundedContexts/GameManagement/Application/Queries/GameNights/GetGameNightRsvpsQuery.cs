using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get RSVPs for a game night.
/// Issue #46: GameNight API endpoints.
/// #2698: <see cref="CallerUserId"/> scopes the roster read to participants (organizer or invited),
/// closing the cross-tenant IDOR on the RSVP roster.
/// </summary>
internal record GetGameNightRsvpsQuery(Guid GameNightId, Guid CallerUserId) : IQuery<IReadOnlyList<GameNightRsvpDto>>;
