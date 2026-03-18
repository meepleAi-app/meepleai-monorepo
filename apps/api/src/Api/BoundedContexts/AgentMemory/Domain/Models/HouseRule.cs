using Api.BoundedContexts.AgentMemory.Domain.Enums;

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a house rule added to a game's memory.
/// </summary>
internal sealed class HouseRule
{
    public string Description { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public HouseRuleSource Source { get; set; }
}
