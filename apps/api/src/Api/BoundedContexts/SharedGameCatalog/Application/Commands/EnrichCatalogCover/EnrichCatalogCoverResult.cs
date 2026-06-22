namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Discriminated outcome of <see cref="EnrichCatalogCoverCommand"/>. Distinguishes
/// pipeline success from expected business skips (e.g. QID missing, license not
/// whitelisted) and unexpected technical failures (image processing or R2
/// upload errors).
/// </summary>
/// <remarks>
/// Pattern matches CLAUDE.md pitfall #2568: handlers MUST NOT throw
/// <see cref="System.InvalidOperationException"/> for business outcomes. Only
/// <see cref="Api.Middleware.Exceptions.NotFoundException"/> /
/// <see cref="Api.Middleware.Exceptions.ConflictException"/> are allowed as
/// route-mapped exceptions; everything else collapses into one of the three
/// variants below.
/// </remarks>
internal abstract record EnrichCatalogCoverResult
{
    /// <summary>Private constructor — prevents external inheritance.</summary>
    private protected EnrichCatalogCoverResult() { }

    /// <summary>
    /// Successful enrichment: the four cover columns + <c>WikidataQidLastVerifiedAt</c>
    /// are now persisted on the aggregate.
    /// </summary>
    /// <param name="R2Key">Deterministic R2 key WITHOUT the <c>.webp</c> suffix (CoverUrlResolver appends it).</param>
    /// <param name="License">Whitelisted Commons license string (e.g. <c>"CC BY-SA 4.0"</c>).</param>
    /// <param name="Attribution">Plain-text Artist credit, or <see langword="null"/> for CC0 / PD works.</param>
    /// <param name="SourceUrl">Canonical Wikidata entity URL used as the attribution back-link.</param>
    public sealed record Success(
        string R2Key,
        string License,
        string? Attribution,
        string SourceUrl) : EnrichCatalogCoverResult;

    /// <summary>
    /// Skipped for an expected business reason — the orchestrator decided NOT
    /// to enrich (e.g. QID missing, license not whitelisted, recently enriched).
    /// Not retriable until the underlying state changes.
    /// </summary>
    /// <param name="Reason">Stable, machine-readable skip reason (used for metrics tagging).</param>
    public sealed record Skipped(string Reason) : EnrichCatalogCoverResult;

    /// <summary>
    /// Failed due to a technical error (image processing, R2 upload).
    /// Idempotent retry is safe — the deterministic R2 key means a successful
    /// re-run overwrites cleanly.
    /// </summary>
    /// <param name="Reason">Stable, machine-readable failure category (used for metrics tagging).</param>
    /// <param name="Details">Optional human-readable detail (e.g. exception message).</param>
    public sealed record Failed(string Reason, string? Details) : EnrichCatalogCoverResult;
}
