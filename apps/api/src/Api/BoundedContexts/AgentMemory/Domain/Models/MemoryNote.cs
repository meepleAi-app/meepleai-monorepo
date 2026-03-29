namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a note attached to a game's memory.
/// </summary>
internal sealed class MemoryNote
{
    private MemoryNote() { } // Required for JSON deserialization

    public string Content { get; private set; } = string.Empty;
    public DateTime AddedAt { get; private set; }
    public Guid? AddedByUserId { get; private set; }

    public static MemoryNote Create(string content, Guid? addedByUserId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new MemoryNote
        {
            Content = content,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = addedByUserId
        };
    }

    /// <summary>
    /// Restores from persistence with the original AddedAt timestamp.
    /// </summary>
    internal static MemoryNote Restore(string content, DateTime addedAt, Guid? addedByUserId)
    {
        return new MemoryNote
        {
            Content = content,
            AddedAt = addedAt,
            AddedByUserId = addedByUserId
        };
    }
}
