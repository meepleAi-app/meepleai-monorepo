using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to check if an entity is in the user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal record GetCollectionStatusQuery(
    Guid UserId,
    EntityType EntityType,
    Guid EntityId
) : IQuery<CollectionStatusDto>;
