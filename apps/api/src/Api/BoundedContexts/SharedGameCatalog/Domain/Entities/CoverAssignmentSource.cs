namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// The cover sources an admin may pin to a UI context (epic #3470, Slice 0 SD1).
/// Excludes the per-user custom cover (L3), which is user personalization and is
/// never set at catalog level. Maps to the internal storage-key
/// <c>CoverKind</c> in the infrastructure layer.
/// </summary>
public enum CoverAssignmentSource
{
    /// <summary>PDF-derived cover (rulebook page).</summary>
    Pdf = 0,

    /// <summary>BGG re-uploaded cover (admin server-to-server, ADR-059 §2).</summary>
    Bgg = 1,

    /// <summary>Wikidata/Commons cover.</summary>
    Wikidata = 2,

    /// <summary>Admin manual-URL cover, fetched + re-hosted on R2 with license attestation.</summary>
    Manual = 3,
}
