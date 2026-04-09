using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to assign or update an entity in a user's hand slot.
/// </summary>
internal record UpdateHandSlotCommand(
    Guid UserId,
    string SlotType,       // "toolkit" | "game" | "session" | "ai"
    Guid EntityId,
    string EntityType,     // "toolkit" | "game" | "session" | "agent"
    string? EntityLabel = null,
    string? EntityImageUrl = null
) : ICommand<UserHandSlotDto>;
