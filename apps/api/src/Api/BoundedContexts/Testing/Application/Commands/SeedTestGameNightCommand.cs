using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Seed a GameNightEvent for E2E test data-driven
/// scenarios. Stamps the new aggregate with the caller's TestRunId via explicit column
/// so CleanupTestEntitiesCommand can cascade-delete by run scope.
///
/// Issue #1929 Task C Macro 4 (DEC-C-10 PIVOT) — Added optional <see cref="GameId"/> param
/// to link the seeded GameNight to a SharedGame catalog entry created via
/// <see cref="SeedTestLibraryGameCommand"/>. When provided, the handler stores the GameId
/// in <c>GameNightEventEntity.GameIdsJson</c> so the night is associated with the game.
/// </summary>
public sealed record SeedTestGameNightCommand : IRequest<SeedTestGameNightResponse>
{
    public required string TestRunId { get; init; }
    public required string Status { get; init; }
    public required string OwnerEmail { get; init; }
    public string? ScoringType { get; init; }
    public int RosterCount { get; init; }

    /// <summary>
    /// Optional SharedGame catalog id to associate this GameNight with.
    /// Must exist in <c>shared_games</c> when provided. Format validated by
    /// <see cref="SeedTestGameNightCommandValidator"/> (non-empty Guid);
    /// existence validated in handler via <c>BadRequestException</c>.
    /// When provided, stored in <c>GameNightEventEntity.GameIdsJson</c>.
    /// </summary>
    public Guid? GameId { get; init; }
}
