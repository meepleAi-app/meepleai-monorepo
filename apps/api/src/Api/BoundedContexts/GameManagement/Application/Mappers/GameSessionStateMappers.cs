using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Application.Mappers;

/// <summary>
/// Extension methods for mapping GameSessionState domain entities to DTOs.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal static class GameSessionStateMappers
{
    public static GameSessionStateDto ToDto(this GameSessionState state)
    {
        return new GameSessionStateDto
        {
            Id = state.Id,
            GameSessionId = state.GameSessionId,
            TemplateId = state.TemplateId,
            CurrentState = state.CurrentState,
            Version = state.Version,
            LastUpdatedAt = state.LastUpdatedAt,
            LastUpdatedBy = state.LastUpdatedBy,
            Snapshots = state.Snapshots.Select(s => s.ToDto()).ToList()
        };
    }

    public static GameStateSnapshotDto ToDto(this GameStateSnapshot snapshot)
    {
        return new GameStateSnapshotDto
        {
            Id = snapshot.Id,
            SessionStateId = snapshot.SessionStateId,
            State = snapshot.State,
            TurnNumber = snapshot.TurnNumber,
            Description = snapshot.Description,
            CreatedAt = snapshot.CreatedAt,
            CreatedBy = snapshot.CreatedBy
        };
    }
}
