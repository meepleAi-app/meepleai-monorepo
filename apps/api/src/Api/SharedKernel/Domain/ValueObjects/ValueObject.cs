namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Base class for value objects in DDD.
/// Value objects are immutable and have no identity.
/// Equality is based on the values of all properties.
/// </summary>
/// <remarks>
/// This class implements IEquatable{T} but is intentionally left abstract (not sealed)
/// to support DDD value object hierarchies. The equality contract is fixed to structural comparison
/// via GetEqualityComponents() and should not be overridden to maintain value object semantics.
/// S4035 is suppressed as this design is intentional for DDD base classes.
/// </remarks>
#pragma warning disable S4035 // Classes implementing "IEquatable<T>" should be sealed or implement "IEqualityComparer<T>"
internal abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Gets the atomic values that define this value object's equality.
    /// Override this in derived classes to specify which properties define equality.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
    {
        return obj is ValueObject valueObject && Equals(valueObject);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
#pragma warning restore S4035
