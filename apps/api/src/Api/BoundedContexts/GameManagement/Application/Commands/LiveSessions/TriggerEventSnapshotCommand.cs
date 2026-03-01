using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Triggers an event-based snapshot with debounce protection.
/// Returns null if the snapshot was debounced (skipped).
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed record TriggerEventSnapshotCommand(
    Guid SessionId,
    SnapshotTrigger TriggerType,
    string? Description,
    Guid? CreatedByPlayerId) : ICommand<SessionSnapshotDto?>;
