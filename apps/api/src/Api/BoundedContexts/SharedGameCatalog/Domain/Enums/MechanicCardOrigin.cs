namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Provenance of a <see cref="Aggregates.MechanicCard"/> (ADR-051). Persisted as a lowercase
/// snake_case string in the <c>mechanic_cards.origin</c> column (DB CHECK constraint enforces the
/// same set).
/// </summary>
public enum MechanicCardOrigin
{
    /// <summary>Promoted from an AI-generated analysis that passed admin review (#526 → #527).</summary>
    AiReviewed = 0,

    /// <summary>Authored manually by an admin (no AI pipeline). Reserved for future manual entry.</summary>
    Manual = 1,

    /// <summary>Imported from an external certified source. Reserved for future B2B ingestion.</summary>
    ImportedExternal = 2
}
