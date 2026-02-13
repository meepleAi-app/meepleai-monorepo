using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to add an entity to user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal record AddToCollectionCommand(
    Guid UserId,
    EntityType EntityType,
    Guid EntityId,
    bool IsFavorite = false,
    string? Notes = null
) : ICommand;
