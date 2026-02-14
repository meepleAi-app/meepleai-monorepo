namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for generic collection status (any entity type).
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
/// <param name="InCollection">Whether the entity is in the user's collection.</param>
/// <param name="IsFavorite">Whether the entity is marked as favorite.</param>
/// <param name="AssociatedData">Associated data that will be lost if removed. Null if not in collection.</param>
internal record CollectionStatusDto(
    bool InCollection,
    bool IsFavorite,
    AssociatedDataDto? AssociatedData = null
);
