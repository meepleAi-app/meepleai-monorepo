using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a frequently asked question for a game.
/// FAQs provide clarifications and help users understand game rules better.
/// </summary>
public sealed class GameFaq : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private readonly string _question = string.Empty;
    private readonly string _answer = string.Empty;
    private int _order;
    private int _upvoteCount;
    private readonly DateTime _createdAt;
    private DateTime? _updatedAt;

    /// <summary>
    /// Gets the unique identifier of this FAQ.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the game this FAQ belongs to.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets the question text.
    /// </summary>
    public string Question => _question;

    /// <summary>
    /// Gets the answer text.
    /// </summary>
    public string Answer => _answer;

    /// <summary>
    /// Gets the display order of this FAQ.
    /// </summary>
    public int Order => _order;

    /// <summary>
    /// Gets the number of upvotes for this FAQ.
    /// </summary>
    public int UpvoteCount => _upvoteCount;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    public DateTime? UpdatedAt => _updatedAt;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameFaq() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameFaq(
        Guid id,
        Guid sharedGameId,
        string question,
        string answer,
        int order,
        int upvoteCount,
        DateTime createdAt,
        DateTime? updatedAt) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _question = question;
        _answer = answer;
        _order = order;
        _upvoteCount = upvoteCount;
        _createdAt = createdAt;
        _updatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new GameFaq with validation.
    /// </summary>
    public static GameFaq Create(
        Guid sharedGameId,
        string question,
        string answer,
        int order)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required", nameof(question));

        if (question.Length > 500)
            throw new ArgumentException("Question cannot exceed 500 characters", nameof(question));

        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer is required", nameof(answer));

        if (order < 0)
            throw new ArgumentException("Order cannot be negative", nameof(order));

        return new GameFaq(
            Guid.NewGuid(),
            sharedGameId,
            question,
            answer,
            order,
            upvoteCount: 0,
            DateTime.UtcNow,
            updatedAt: null);
    }

    /// <summary>
    /// Updates the FAQ order.
    /// </summary>
    public void UpdateOrder(int newOrder)
    {
        if (newOrder < 0)
            throw new ArgumentException("Order cannot be negative", nameof(newOrder));

        _order = newOrder;
        _updatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments the upvote count for this FAQ.
    /// </summary>
    /// <returns>The new upvote count.</returns>
    public int Upvote()
    {
        _upvoteCount++;
        _updatedAt = DateTime.UtcNow;
        return _upvoteCount;
    }
}
