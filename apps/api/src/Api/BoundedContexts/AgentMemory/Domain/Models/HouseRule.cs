using Api.BoundedContexts.AgentMemory.Domain.Enums;

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a house rule added to a game's memory.
/// </summary>
internal sealed class HouseRule
{
    private HouseRule() { } // Required for JSON deserialization

    /// <summary>
    /// Stable identifier (#1464). Generated on Create; rules persisted before #1464
    /// (without an Id in their JSON blob) get a fresh Guid at Restore time so they
    /// can be referenced by PATCH/DELETE endpoints.
    /// </summary>
    public Guid Id { get; private set; }

    public string Description { get; private set; } = string.Empty;
    public DateTime AddedAt { get; private set; }
    public HouseRuleSource Source { get; private set; }

    public static HouseRule Create(string description, HouseRuleSource source)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        return new HouseRule
        {
            Id = Guid.NewGuid(),
            Description = description,
            AddedAt = DateTime.UtcNow,
            Source = source
        };
    }

    /// <summary>
    /// Restores from persistence with the original AddedAt timestamp.
    /// If the persisted Id is empty (legacy pre-#1464), a fresh Guid is assigned.
    /// </summary>
    internal static HouseRule Restore(Guid id, string description, DateTime addedAt, HouseRuleSource source)
    {
        return new HouseRule
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            Description = description,
            AddedAt = addedAt,
            Source = source
        };
    }

    /// <summary>Mutates the description in-place. Used by GameMemory.UpdateHouseRule.</summary>
    internal void UpdateDescription(string newDescription)
    {
        if (string.IsNullOrWhiteSpace(newDescription))
            throw new ArgumentException("Description cannot be empty.", nameof(newDescription));
        Description = newDescription;
    }
}
