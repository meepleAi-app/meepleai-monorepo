using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to update a group's gaming preferences.
/// </summary>
internal record UpdateGroupPreferencesCommand(
    Guid GroupId,
    TimeSpan? MaxDuration,
    string? PreferredComplexity,
    string? CustomNotes
) : ICommand;
