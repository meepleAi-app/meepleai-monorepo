namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a single append-only diary entry in a live game session.
/// Multi-author public diary — distinct from the host-level <c>Notes</c> single-string field.
/// Intended to be persisted as a separate owned table (live_session_diary_entries) in T2.
/// </summary>
internal sealed record DiaryEntry
{
    /// <summary>Unique identifier for this diary entry.</summary>
    public Guid Id { get; init; }

    /// <summary>UserId of the player who authored the entry.</summary>
    public Guid AuthorId { get; init; }

    /// <summary>UTC timestamp when the entry was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The diary entry text (trimmed, non-empty).</summary>
    public string Text { get; init; }

    /// <summary>
    /// Creates a new <see cref="DiaryEntry"/>.
    /// </summary>
    public DiaryEntry(Guid id, Guid authorId, DateTimeOffset createdAt, string text)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Diary entry ID cannot be empty.", nameof(id));

        if (authorId == Guid.Empty)
            throw new ArgumentException("Author ID cannot be empty.", nameof(authorId));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Diary entry text cannot be empty.", nameof(text));

        Id = id;
        AuthorId = authorId;
        CreatedAt = createdAt;
        Text = text.Trim();
    }
}
