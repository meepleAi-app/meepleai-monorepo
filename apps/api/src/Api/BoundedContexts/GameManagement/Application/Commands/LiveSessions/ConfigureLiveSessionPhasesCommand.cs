using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Configures turn phases on a live session from a toolkit's turn template.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed record ConfigureLiveSessionPhasesCommand(
    Guid SessionId,
    string[] PhaseNames) : ICommand;
