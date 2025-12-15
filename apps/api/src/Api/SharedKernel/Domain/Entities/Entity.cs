using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Domain.Entities;

/// <summary>
/// Base class for all entities in the domain model.
/// Provides equality comparison based on ID.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
/// <remarks>
/// This class implements IEquatable{T} but is intentionally left abstract (not sealed)
/// to support DDD aggregate hierarchies. The equality contract is fixed to ID-based comparison
/// and should not be overridden in derived classes to maintain domain model integrity.
/// S4035 is suppressed as this design is intentional for DDD base classes.
/// </remarks>
#pragma warning disable S4035 // Classes implementing "IEquatable<T>" should be sealed or implement "IEqualityComparer<T>"
internal abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TId}"/> class.
    /// </summary>
    /// <param name="id">The entity's unique identifier</param>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    protected Entity()
#pragma warning restore CS8618
    {
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}
#pragma warning restore S4035
