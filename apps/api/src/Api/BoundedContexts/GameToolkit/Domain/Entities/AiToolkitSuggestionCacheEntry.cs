using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Cached AiToolkit suggestion per game. ADR-069 follow-up (#2383).
/// One row per game (UNIQUE on game_id). Invalidated by
/// <c>InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler</c>
/// when <c>KbDocIndexedEvent</c> fires for the same game.
/// </summary>
internal sealed class AiToolkitSuggestionCacheEntry : AggregateRoot<Guid>
{
    /// <summary>
    /// The shared game this cached suggestion belongs to.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// Serialized <c>AiToolkitSuggestionDto</c> payload (camelCase JSON).
    /// </summary>
    public string SuggestionJson { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the suggestion was generated or last refreshed.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>
    /// Optional: KB document version at the time of generation.
    /// Nullable — KbVersion enforcement is out of scope for this iteration (ADR-069 §Out of scope).
    /// </summary>
    public int? KbVersion { get; private set; }

#pragma warning disable CS8618
    private AiToolkitSuggestionCacheEntry() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Factory method: creates a new cache entry for the given game.
    /// </summary>
    /// <param name="gameId">The shared game id. Must not be empty.</param>
    /// <param name="suggestionJson">Serialized <c>AiToolkitSuggestionDto</c>. Must not be null/empty.</param>
    /// <param name="kbVersion">Optional KB doc version at generation time.</param>
    public static AiToolkitSuggestionCacheEntry Create(Guid gameId, string suggestionJson, int? kbVersion)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(suggestionJson))
            throw new ArgumentException("suggestion payload cannot be empty.", nameof(suggestionJson));

        return new AiToolkitSuggestionCacheEntry
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            SuggestionJson = suggestionJson,
            KbVersion = kbVersion,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates the cached payload and bumps the <see cref="GeneratedAt"/> timestamp.
    /// Call this when a new LLM result is available for the same game.
    /// </summary>
    /// <param name="suggestionJson">The new serialized payload. Must not be null/empty.</param>
    /// <param name="kbVersion">Optional KB doc version at generation time.</param>
    public void Refresh(string suggestionJson, int? kbVersion)
    {
        if (string.IsNullOrWhiteSpace(suggestionJson))
            throw new ArgumentException("suggestion payload cannot be empty.", nameof(suggestionJson));
        SuggestionJson = suggestionJson;
        KbVersion = kbVersion;
        GeneratedAt = DateTimeOffset.UtcNow;
    }
}
