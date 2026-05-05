using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to add a glossary entry to a game's memory. Creates GameMemory if none exists.
/// </summary>
internal record AddGlossaryEntryCommand(
    Guid GameId,
    Guid OwnerId,
    string Term,
    string Definition,
    string Language,
    GlossaryEntrySource Source = GlossaryEntrySource.UserDefined
) : ICommand;
