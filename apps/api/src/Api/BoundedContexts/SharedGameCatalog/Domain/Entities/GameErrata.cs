using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing an erratum for a game.
/// Errata document rule corrections or clarifications.
/// </summary>
public sealed class GameErrata : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private readonly string _description = string.Empty;
    private readonly string _pageReference = string.Empty;
    private readonly DateTime _publishedDate;
    private readonly DateTime _createdAt;

    /// <summary>
    /// Gets the unique identifier of this erratum.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the game this erratum belongs to.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets the description of the erratum.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the page reference in the rulebook.
    /// </summary>
    public string PageReference => _pageReference;

    /// <summary>
    /// Gets the date this erratum was published.
    /// </summary>
    public DateTime PublishedDate => _publishedDate;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameErrata() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameErrata(
        Guid id,
        Guid sharedGameId,
        string description,
        string pageReference,
        DateTime publishedDate,
        DateTime createdAt) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _description = description;
        _pageReference = pageReference;
        _publishedDate = publishedDate;
        _createdAt = createdAt;
    }

    /// <summary>
    /// Creates a new GameErrata with validation.
    /// </summary>
    public static GameErrata Create(
        Guid sharedGameId,
        string description,
        string pageReference,
        DateTime publishedDate)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (string.IsNullOrWhiteSpace(pageReference))
            throw new ArgumentException("PageReference is required", nameof(pageReference));

        if (pageReference.Length > 100)
            throw new ArgumentException("PageReference cannot exceed 100 characters", nameof(pageReference));

        if (publishedDate > DateTime.UtcNow)
            throw new ArgumentException("PublishedDate cannot be in the future", nameof(publishedDate));

        return new GameErrata(
            Guid.NewGuid(),
            sharedGameId,
            description,
            pageReference,
            publishedDate,
            DateTime.UtcNow);
    }
}
