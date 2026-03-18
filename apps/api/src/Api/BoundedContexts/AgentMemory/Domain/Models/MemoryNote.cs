namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a note attached to a game's memory.
/// </summary>
internal sealed class MemoryNote
{
    public string Content { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public Guid? AddedByUserId { get; set; }
}
