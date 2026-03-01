namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Aggregate for game strategy tips, tactics, and winning guides.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal sealed class GameStrategy
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Cross-BC reference to SharedGameCatalog.SharedGame.Id.
    /// </summary>
    public Guid SharedGameId { get; private set; }

    public string Title { get; private set; }
    public string Content { get; private set; }
    public string Author { get; private set; }
    public int Upvotes { get; private set; }

    /// <summary>
    /// Tags stored as JSON string in the database.
    /// </summary>
    public IReadOnlyList<string> Tags { get; private set; }

    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private GameStrategy() { }
#pragma warning restore CS8618

    private GameStrategy(
        Guid id,
        Guid sharedGameId,
        string title,
        string content,
        string author,
        int upvotes,
        IReadOnlyList<string> tags,
        DateTime createdAt)
    {
        Id = id;
        SharedGameId = sharedGameId;
        Title = title;
        Content = content;
        Author = author;
        Upvotes = upvotes;
        Tags = tags;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Reconstructs a GameStrategy from persistence data.
    /// </summary>
    internal static GameStrategy Reconstitute(
        Guid id,
        Guid sharedGameId,
        string title,
        string content,
        string author,
        int upvotes,
        IReadOnlyList<string> tags,
        DateTime createdAt)
    {
        return new GameStrategy(id, sharedGameId, title, content, author, upvotes, tags, createdAt);
    }
}
