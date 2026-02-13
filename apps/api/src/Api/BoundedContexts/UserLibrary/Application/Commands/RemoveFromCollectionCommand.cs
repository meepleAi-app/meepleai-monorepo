using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to remove an entity from user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal record RemoveFromCollectionCommand(
    Guid UserId,
    EntityType EntityType,
    Guid EntityId
) : ICommand;
