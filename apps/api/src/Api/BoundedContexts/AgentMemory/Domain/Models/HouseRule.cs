using Api.BoundedContexts.AgentMemory.Domain.Enums;

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a house rule added to a game's memory.
/// </summary>
internal sealed class HouseRule
{
    private HouseRule() { } // Required for JSON deserialization

    public string Description { get; private set; } = string.Empty;
    public DateTime AddedAt { get; private set; }
    public HouseRuleSource Source { get; private set; }

    public static HouseRule Create(string description, HouseRuleSource source)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        return new HouseRule
        {
            Description = description,
            AddedAt = DateTime.UtcNow,
            Source = source
        };
    }

    /// <summary>
    /// Restores from persistence with the original AddedAt timestamp.
    /// </summary>
    internal static HouseRule Restore(string description, DateTime addedAt, HouseRuleSource source)
    {
        return new HouseRule
        {
            Description = description,
            AddedAt = addedAt,
            Source = source
        };
    }
}
