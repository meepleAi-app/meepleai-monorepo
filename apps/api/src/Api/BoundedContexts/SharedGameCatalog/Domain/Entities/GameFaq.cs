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
    private readonly DateTime _createdAt;

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
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

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
        DateTime createdAt) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _question = question;
        _answer = answer;
        _order = order;
        _createdAt = createdAt;
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
            DateTime.UtcNow);
    }

    /// <summary>
    /// Updates the FAQ order.
    /// </summary>
    public void UpdateOrder(int newOrder)
    {
        if (newOrder < 0)
            throw new ArgumentException("Order cannot be negative", nameof(newOrder));

        _order = newOrder;
    }
}
