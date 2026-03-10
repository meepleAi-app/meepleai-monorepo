using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get RSVPs for a game night.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record GetGameNightRsvpsQuery(Guid GameNightId) : IQuery<IReadOnlyList<GameNightRsvpDto>>;
