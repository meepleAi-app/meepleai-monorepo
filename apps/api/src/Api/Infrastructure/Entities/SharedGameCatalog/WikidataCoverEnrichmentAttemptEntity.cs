namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence model for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.WikidataCoverEnrichmentAttempt"/>.
/// Issue #1823 Wave 3 M9.
/// </summary>
public class WikidataCoverEnrichmentAttemptEntity
{
    public Guid Id { get; set; }

    public Guid SharedGameId { get; set; }

    public DateTime AttemptedAt { get; set; }

    /// <summary>Numeric mapping of <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.WikidataCoverEnrichmentOutcome"/>.</summary>
    public int Outcome { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Details { get; set; }

    public int RetryCount { get; set; }

    /// <summary>UTC timestamp when the scheduler may re-run; null for terminal outcomes.</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>UTC timestamp when the attempt was dead-lettered; null otherwise.</summary>
    public DateTime? DeadLetteredAt { get; set; }
}
