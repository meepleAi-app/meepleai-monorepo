namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Base interface for all entities in the domain model.
/// Entities have identity and are distinguished by their ID, not by their attributes.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
/// <remarks>
/// TId is covariant (out) because it only appears in output position (property getter).
/// This allows IEntity{Guid} to be assignable to IEntity{object}, improving type safety.
/// </remarks>
internal interface IEntity<out TId> where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    TId Id { get; }
}
