using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to clear (remove) a user's hand slot assignment.
/// </summary>
internal record ClearHandSlotCommand(Guid UserId, string SlotType) : ICommand;
