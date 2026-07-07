using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to update a group's gaming preferences.
/// </summary>
/// <param name="GroupId">Group whose preferences to update.</param>
/// <param name="RequesterId">Authenticated caller — must be the creator or a member (#2655 IDOR guard).</param>
/// <param name="MaxDuration">Preferred maximum session duration, if any.</param>
/// <param name="PreferredComplexity">Preferred game complexity (Light/Medium/Heavy), if any.</param>
/// <param name="CustomNotes">Free-form group preference notes, if any.</param>
internal record UpdateGroupPreferencesCommand(
    Guid GroupId,
    Guid RequesterId,
    TimeSpan? MaxDuration,
    string? PreferredComplexity,
    string? CustomNotes
) : ICommand;
