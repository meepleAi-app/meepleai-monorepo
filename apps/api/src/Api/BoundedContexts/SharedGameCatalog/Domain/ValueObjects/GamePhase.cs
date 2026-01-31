using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing a phase in the game turn structure.
/// </summary>
public sealed class GamePhase : ValueObject
{
    /// <summary>
    /// Gets the phase name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the phase description.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the order in which this phase occurs (1-based).
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Gets whether this phase is optional.
    /// </summary>
    public bool IsOptional { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private GamePhase()
    {
        Name = string.Empty;
        Description = string.Empty;
        Order = 1;
    }

    /// <summary>
    /// Private constructor for creating a game phase.
    /// </summary>
    private GamePhase(string name, string description, int order, bool isOptional)
    {
        Name = name;
        Description = description;
        Order = order;
        IsOptional = isOptional;
    }

    /// <summary>
    /// Creates a new GamePhase instance with validation.
    /// </summary>
    public static GamePhase Create(
        string name,
        string description,
        int order,
        bool isOptional = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Phase description is required", nameof(description));

        if (order <= 0)
            throw new ArgumentException("Phase order must be positive", nameof(order));

        return new GamePhase(
            name.Trim(),
            description.Trim(),
            order,
            isOptional);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Description;
        yield return Order;
        yield return IsOptional;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Order}. {Name}";
}
