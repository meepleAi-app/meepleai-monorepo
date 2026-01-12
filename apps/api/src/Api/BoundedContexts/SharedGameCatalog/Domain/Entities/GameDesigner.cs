using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a game designer.
/// Designers are linked to games through many-to-many relationships.
/// </summary>
public sealed class GameDesigner : Entity<Guid>
{
    private Guid _id;
    private readonly string _name = string.Empty;
    private readonly DateTime _createdAt;

    /// <summary>
    /// Gets the unique identifier of this designer.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the full name of the designer.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameDesigner() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameDesigner(Guid id, string name, DateTime createdAt) : base(id)
    {
        _id = id;
        _name = name;
        _createdAt = createdAt;
    }

    /// <summary>
    /// Creates a new GameDesigner with validation.
    /// </summary>
    public static GameDesigner Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Designer name is required", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Designer name cannot exceed 200 characters", nameof(name));

        return new GameDesigner(Guid.NewGuid(), name, DateTime.UtcNow);
    }
}
