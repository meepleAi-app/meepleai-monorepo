using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to claim a guest player's memory for a registered user.
/// Simplified MVP: skips host confirmation.
/// </summary>
internal record ClaimGuestPlayerCommand(
    Guid UserId,
    Guid PlayerMemoryId
) : ICommand;
