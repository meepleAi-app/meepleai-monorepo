using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.Infrastructure.Entities;

public class GameEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Game Details (DDD-PHASE2 GameManagement bounded context)
    public string? Publisher { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? MinPlayTimeMinutes { get; set; }
    public int? MaxPlayTimeMinutes { get; set; }

    // BoardGameGeek integration (AI-13)
    public int? BggId { get; set; }
    public string? BggMetadata { get; set; } // JSONB column storing raw BGG API response

    // Admin Wizard: Game images
    public string? IconUrl { get; set; }
    public string? ImageUrl { get; set; }

    // PDF Upload: Version tracking (Issue: Game auto-creation)
    /// <summary>
    /// Type of game version: base, expansion, errata, home_rule
    /// </summary>
    public string? VersionType { get; set; }

    /// <summary>
    /// Language code: it, en, de, fr, es, etc.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Version number: 1.0, 2.0, 1.5, etc.
    /// </summary>
    public string? VersionNumber { get; set; }

    // Issue #2373 Phase 4: SharedGameCatalog integration
    /// <summary>
    /// Optional link to SharedGameCatalog for enhanced game details.
    /// When set, the game is linked to community-curated content (rules, FAQs, errata).
    /// </summary>
    public Guid? SharedGameId { get; set; }
    public SharedGameEntity? SharedGame { get; set; }

    // Issue #3481: Publication workflow
    /// <summary>
    /// Indicates if game is published to SharedGameCatalog.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Approval status for SharedGameCatalog publication.
    /// Values: 0=Draft, 1=PendingReview, 2=Approved, 3=Rejected
    /// </summary>
    public int ApprovalStatus { get; set; }

    /// <summary>
    /// Timestamp when game was approved and published.
    /// Null if not approved.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    public ICollection<RuleSpecEntity> RuleSpecs { get; set; } = new List<RuleSpecEntity>();
    public ICollection<ChatEntity> Chats { get; set; } = new List<ChatEntity>();
}