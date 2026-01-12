using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a game publisher.
/// Publishers are linked to games through many-to-many relationships.
/// </summary>
public sealed class GamePublisher : Entity<Guid>
{
    private Guid _id;
    private readonly string _name = string.Empty;
    private readonly DateTime _createdAt;

    /// <summary>
    /// Gets the unique identifier of this publisher.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the full name of the publisher.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GamePublisher() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GamePublisher(Guid id, string name, DateTime createdAt) : base(id)
    {
        _id = id;
        _name = name;
        _createdAt = createdAt;
    }

    /// <summary>
    /// Creates a new GamePublisher with validation.
    /// </summary>
    public static GamePublisher Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Publisher name is required", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Publisher name cannot exceed 200 characters", nameof(name));

        return new GamePublisher(Guid.NewGuid(), name, DateTime.UtcNow);
    }
}
