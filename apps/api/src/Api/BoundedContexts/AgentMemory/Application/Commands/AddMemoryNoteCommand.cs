using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to add a note to a game's memory. Creates GameMemory if none exists.
/// </summary>
internal record AddMemoryNoteCommand(
    Guid GameId,
    Guid OwnerId,
    string Content
) : ICommand;
