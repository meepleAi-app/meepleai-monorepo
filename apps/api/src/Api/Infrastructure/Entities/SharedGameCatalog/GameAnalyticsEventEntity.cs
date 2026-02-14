namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core persistence entity for game analytics events.
/// Tracks user interactions with games for trending calculation.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
public class GameAnalyticsEventEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int EventType { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}
