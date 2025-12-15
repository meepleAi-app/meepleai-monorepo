using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// GameFAQ aggregate root representing a frequently asked question for a game.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal sealed class GameFAQ : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public FAQQuestion Question { get; private set; }
    public FAQAnswer Answer { get; private set; }
    public int Upvotes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameFAQ() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new FAQ for a game.
    /// </summary>
    public GameFAQ(
        Guid id,
        Guid gameId,
        FAQQuestion question,
        FAQAnswer answer) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        GameId = gameId;
        Question = question ?? throw new ArgumentNullException(nameof(question));
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        Upvotes = 0;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the FAQ question and answer.
    /// </summary>
    public void Update(FAQQuestion question, FAQAnswer answer)
    {
        Question = question ?? throw new ArgumentNullException(nameof(question));
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments the upvote count.
    /// </summary>
    public void AddUpvote()
    {
        if (Upvotes >= int.MaxValue - 1)
            throw new InvalidOperationException("Maximum upvotes reached");

        Upvotes++;
    }

    /// <summary>
    /// Decrements the upvote count (if greater than 0).
    /// </summary>
    public void RemoveUpvote()
    {
        if (Upvotes > 0)
            Upvotes--;
    }
}
