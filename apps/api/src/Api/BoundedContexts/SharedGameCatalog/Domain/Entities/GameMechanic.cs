using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a game mechanic (e.g., Deck Building, Worker Placement).
/// Mechanics describe how the game is played and are used for filtering.
/// </summary>
public sealed class GameMechanic : Entity<Guid>
{
    private Guid _id;
    private readonly string _name = string.Empty;
    private readonly string _slug = string.Empty;

    /// <summary>
    /// Gets the unique identifier of this mechanic.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the display name of the mechanic.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the URL-friendly slug of the mechanic.
    /// </summary>
    public string Slug => _slug;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameMechanic() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameMechanic(Guid id, string name, string slug) : base(id)
    {
        _id = id;
        _name = name;
        _slug = slug;
    }

    /// <summary>
    /// Creates a new GameMechanic with validation.
    /// </summary>
    public static GameMechanic Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Mechanic name is required", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Mechanic name cannot exceed 100 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Mechanic slug is required", nameof(slug));

        if (slug.Length > 100)
            throw new ArgumentException("Mechanic slug cannot exceed 100 characters", nameof(slug));

        return new GameMechanic(Guid.NewGuid(), name, slug);
    }
}
