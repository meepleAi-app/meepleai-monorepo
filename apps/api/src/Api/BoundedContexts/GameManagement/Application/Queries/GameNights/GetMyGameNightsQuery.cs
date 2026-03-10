using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get game nights where the user is organizer or invited.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record GetMyGameNightsQuery(Guid UserId) : IQuery<IReadOnlyList<GameNightDto>>;
