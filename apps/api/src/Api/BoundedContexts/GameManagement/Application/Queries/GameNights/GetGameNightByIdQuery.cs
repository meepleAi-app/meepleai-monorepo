using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get a game night by its ID.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record GetGameNightByIdQuery(Guid GameNightId) : IQuery<GameNightDto>;
