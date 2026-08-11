namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Per-user, per-claim feedback on a published mechanic card (#533 ME-M3.1). A user has AT MOST one
/// feedback row per (card, claim) — the unique index makes submission idempotent (a user can CHANGE
/// their feedback but never duplicate it). Downstream auto-suppression (#534) aggregates these rows.
/// </summary>
public class MechanicCardFeedbackEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → mechanic_cards.id (the card being rated).</summary>
    public Guid CardId { get; set; }

    /// <summary>The authenticated user who submitted the feedback.</summary>
    public Guid UserId { get; set; }

    /// <summary>The specific claim within the card the feedback targets.</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Thumbs up (true) / thumbs down (false).</summary>
    public bool IsPositive { get; set; }

    /// <summary>Error category, only meaningful for a 👎: 'factual' | 'ambiguous' | 'contradicts_rule'. Null for 👍.</summary>
    public string? ErrorType { get; set; }

    /// <summary>Optional free-text description of the reported problem.</summary>
    public string? Description { get; set; }

    /// <summary>Optional citation to the correct rule the user believes applies.</summary>
    public string? SuggestedCitation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // === Navigation ===
    public MechanicCardEntity Card { get; set; } = default!;
}
