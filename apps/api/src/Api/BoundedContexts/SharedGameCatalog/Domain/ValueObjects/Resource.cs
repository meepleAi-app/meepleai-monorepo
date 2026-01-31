using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing a game resource (e.g., wood, gold, cards).
/// </summary>
public sealed class Resource : ValueObject
{
    /// <summary>
    /// Gets the resource name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the resource type category.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// Gets the description of how this resource is used.
    /// </summary>
    public string? Usage { get; private set; }

    /// <summary>
    /// Gets whether this resource is limited in quantity.
    /// </summary>
    public bool IsLimited { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Resource()
    {
        Name = string.Empty;
        Type = string.Empty;
    }

    /// <summary>
    /// Private constructor for creating a resource.
    /// </summary>
    private Resource(string name, string type, string? usage, bool isLimited)
    {
        Name = name;
        Type = type;
        Usage = usage;
        IsLimited = isLimited;
    }

    /// <summary>
    /// Creates a new Resource instance with validation.
    /// </summary>
    public static Resource Create(
        string name,
        string type,
        string? usage = null,
        bool isLimited = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Resource type is required", nameof(type));

        return new Resource(
            name.Trim(),
            type.Trim(),
            usage?.Trim(),
            isLimited);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Type;
        if (Usage is not null)
            yield return Usage;
        yield return IsLimited;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({Type})";
}
