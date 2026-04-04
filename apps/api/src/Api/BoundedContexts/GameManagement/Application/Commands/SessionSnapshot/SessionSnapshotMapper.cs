using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

internal static class SessionSnapshotMapper
{
    public static SessionSnapshotDto ToDto(
        Domain.Entities.SessionSnapshot.SessionSnapshot snapshot,
        int attachmentCount = 0)
    {
        return new SessionSnapshotDto(
            Id: snapshot.Id,
            SessionId: snapshot.SessionId,
            SnapshotIndex: snapshot.SnapshotIndex,
            TriggerType: snapshot.TriggerType,
            TriggerDescription: snapshot.TriggerDescription,
            IsCheckpoint: snapshot.IsCheckpoint,
            TurnIndex: snapshot.TurnIndex,
            PhaseIndex: snapshot.PhaseIndex,
            Timestamp: snapshot.Timestamp,
            CreatedByPlayerId: snapshot.CreatedByPlayerId,
            AttachmentCount: attachmentCount);
    }
}
