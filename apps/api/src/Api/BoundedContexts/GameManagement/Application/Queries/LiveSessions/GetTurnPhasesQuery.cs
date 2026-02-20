using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Gets the current turn phases configuration and state for a live session.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed record GetTurnPhasesQuery(Guid SessionId) : IQuery<TurnPhasesDto>;

/// <summary>
/// DTO representing the current phase state of a session.
/// </summary>
internal sealed record TurnPhasesDto(
    int CurrentTurnIndex,
    int CurrentPhaseIndex,
    string? CurrentPhaseName,
    string[] PhaseNames,
    int TotalPhases,
    bool HasPhases);
