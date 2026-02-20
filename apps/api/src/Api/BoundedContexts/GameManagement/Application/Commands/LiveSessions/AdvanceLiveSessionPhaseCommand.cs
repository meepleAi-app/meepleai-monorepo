using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Advances the current turn phase in a live session.
/// If at the last phase, wraps to phase 0.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed record AdvanceLiveSessionPhaseCommand(Guid SessionId) : ICommand;
