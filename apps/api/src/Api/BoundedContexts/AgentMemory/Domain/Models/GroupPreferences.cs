using Api.BoundedContexts.AgentMemory.Domain.Enums;

namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Stores the gaming preferences for a play group.
/// </summary>
internal sealed class GroupPreferences
{
    public TimeSpan? MaxDuration { get; set; }
    public PreferredComplexity? PreferredComplexity { get; set; }
    public string? CustomNotes { get; set; }
}
