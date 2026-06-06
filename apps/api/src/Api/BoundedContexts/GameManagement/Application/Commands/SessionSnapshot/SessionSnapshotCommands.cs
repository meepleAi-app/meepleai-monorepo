using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

/// <summary>
/// Creates a snapshot for a session. Used by both manual saves and automatic triggers.
/// <para>
/// Issue #1938 / CF-2: <c>SourceEventId</c> is propagated to the created snapshot row
/// so the UNIQUE partial index <c>UX_session_snapshots_source_event_id</c> can dedupe
/// at the DB level when the originating event handler is re-dispatched.
/// </para>
/// </summary>
internal sealed record CreateSnapshotCommand(
    Guid SessionId,
    SnapshotTrigger TriggerType,
    string? TriggerDescription,
    Guid? CreatedByPlayerId,
    Guid? SourceEventId = null) : ICommand<SessionSnapshotDto>;
