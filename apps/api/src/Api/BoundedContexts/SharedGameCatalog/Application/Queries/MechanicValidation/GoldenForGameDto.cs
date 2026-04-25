using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Read model returned by <see cref="GetGoldenForGameQuery"/>.
/// Bundles the curated golden-set (mechanic claims + BGG tags) and the version hash
/// representing the current state of that set, scoped to a single shared game.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 27. Consumed by the admin AI Comprehension Validation
/// UI to render the golden-claim list, BGG tag chips, and a "snapshot version" indicator
/// that downstream scoring jobs use to detect stale cached scores.
/// </remarks>
internal sealed record GoldenForGameDto(
    Guid SharedGameId,
    string VersionHash,
    IReadOnlyList<GoldenClaimDto> Claims,
    IReadOnlyList<BggTagDto> BggTags);

/// <summary>
/// Projection of a <see cref="Domain.Aggregates.MechanicGoldenClaim"/> for query responses.
/// Excludes the embedding vector and curator user id, which are internal pipeline concerns.
/// </summary>
internal sealed record GoldenClaimDto(
    Guid Id,
    MechanicSection Section,
    string Statement,
    int ExpectedPage,
    string SourceQuote,
    string[] Keywords,
    DateTimeOffset CreatedAt);

/// <summary>
/// Projection of a <see cref="Domain.Entities.MechanicGoldenBggTag"/> for query responses.
/// </summary>
/// <remarks>
/// Co-located with <see cref="GoldenForGameDto"/> on purpose: keeps query-output shapes
/// independent from the command-side <c>BggTagDto</c> in
/// <c>Application.Commands.Golden</c>, so the two can evolve separately.
/// </remarks>
internal sealed record BggTagDto(string Name, string Category);
