using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

/// <summary>
/// Creates a snapshot for a session. Used by both manual saves and automatic triggers.
/// </summary>
internal sealed record CreateSnapshotCommand(
    Guid SessionId,
    SnapshotTrigger TriggerType,
    string? TriggerDescription,
    Guid? CreatedByPlayerId) : ICommand<SessionSnapshotDto>;
