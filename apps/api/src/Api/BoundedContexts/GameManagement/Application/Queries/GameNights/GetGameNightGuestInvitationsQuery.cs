using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to list the guests invited to a game night by email, with their state.
/// #4706: organizer-only — <see cref="CallerUserId"/> must be the organizer, because the result
/// carries other people's email addresses.
/// </summary>
internal record GetGameNightGuestInvitationsQuery(Guid GameNightId, Guid CallerUserId)
    : IQuery<IReadOnlyList<GameNightGuestInvitationDto>>;
