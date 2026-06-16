namespace Api.Infrastructure.Entities.GameToolkit;

/// <summary>
/// Persistence POCO for <c>AiToolkitSuggestionCacheEntry</c> aggregate.
/// One row per game — UNIQUE constraint enforced by EF configuration and DB index.
/// ADR-069 follow-up (#2383).
/// </summary>
public class AiToolkitSuggestionCacheEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string SuggestionJson { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public int? KbVersion { get; set; }
}
