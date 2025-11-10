namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Base interface for all entities in the domain model.
/// Entities have identity and are distinguished by their ID, not by their attributes.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
public interface IEntity<TId> where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    TId Id { get; }
}
