using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get upcoming published game nights.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record GetUpcomingGameNightsQuery() : IQuery<IReadOnlyList<GameNightDto>>;
