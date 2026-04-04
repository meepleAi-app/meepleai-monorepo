using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to create a new group memory with optional initial members and guests.
/// </summary>
internal record CreateGroupMemoryCommand(
    Guid CreatorId,
    string Name,
    List<Guid>? InitialMemberUserIds = null,
    List<string>? InitialGuestNames = null
) : ICommand<Guid>;
