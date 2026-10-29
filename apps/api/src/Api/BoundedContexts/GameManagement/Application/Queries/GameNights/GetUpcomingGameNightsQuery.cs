using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get the viewer's upcoming published game nights (organized or invited).
/// Issue #46: GameNight API endpoints. #4748: the list is the viewer's, not everyone's.
/// </summary>
internal record GetUpcomingGameNightsQuery(Guid UserId) : IQuery<IReadOnlyList<GameNightDto>>;
