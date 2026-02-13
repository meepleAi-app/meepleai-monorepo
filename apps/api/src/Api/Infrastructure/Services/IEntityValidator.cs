using Api.BoundedContexts.UserLibrary.Domain.Enums;

namespace Api.Infrastructure.Services;

/// <summary>
/// Service for validating entity existence across bounded contexts.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
public interface IEntityValidator
{
    /// <summary>
    /// Validates that an entity exists in the system.
    /// Throws NotFoundException if entity does not exist.
    /// </summary>
    /// <param name="entityType">The type of entity to validate</param>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ValidateEntityExistsAsync(
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
