namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Aggregate statistics for a play group.
/// </summary>
internal sealed class GroupStats
{
    public int TotalSessions { get; set; }
    public Dictionary<Guid, int> GamePlayCounts { get; set; } = new();
    public DateTime? LastPlayedAt { get; set; }
}
