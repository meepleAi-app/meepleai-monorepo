namespace Api.BoundedContexts.AgentMemory.Infrastructure.Entities;

/// <summary>
/// Row of <c>player_memories</c>, the per-player game statistics.
/// </summary>
/// <remarks>
/// #4876: no code reads or writes this table since the expand retired the <c>PlayerMemory</c> aggregate. The entity
/// stays mapped so that the model still matches the schema; the contract drops the table and this entity together.
/// </remarks>
public class PlayerMemoryEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? GuestName { get; set; }
    public Guid? GroupId { get; set; }
    public string? GameStatsJson { get; set; }  // JSONB
    public DateTime? ClaimedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Last domain event id processed against this aggregate (issue #1939 / CF-3).
    /// </summary>
    public Guid? LastProcessedEventId { get; set; }

    /// <summary>JSONB array of the session ids already counted (#4839, option C); null on rows older than the column.</summary>
    public string? ProcessedSessionIdsJson { get; set; }
}
