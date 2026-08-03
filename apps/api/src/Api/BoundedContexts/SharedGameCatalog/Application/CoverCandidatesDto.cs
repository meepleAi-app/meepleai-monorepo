using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Epic #3470 (Slice 1c-3) — the admin cover-picker read shape: the MATERIALIZED
/// source candidates a game can be pinned to (each with a preview URL), plus which
/// source is currently in use per UI context. Unlike <see cref="SharedGameDto.CoverUrl"/>
/// (winner-only), this exposes every available source so the picker can offer a choice.
/// </summary>
public sealed record CoverCandidatesDto(
    Guid GameId,
    IReadOnlyList<CoverCandidateDto> Candidates,
    CoverContextAssignmentsDto Assignments);

/// <summary>
/// A single materialized cover source available to the picker. License / attribution
/// / source URL are populated only for the sources that carry them (Wikidata, Manual).
/// The <c>Source</c> enum serializes as its name (e.g. <c>"Wikidata"</c>) via the global
/// <c>JsonStringEnumConverter</c>.
/// </summary>
public sealed record CoverCandidateDto(
    CoverAssignmentSource Source,
    string PreviewUrl,
    string? License,
    string? Attribution,
    string? SourceUrl);

/// <summary>
/// A single per-context admin assignment: the pinned source plus the persisted focal
/// point (epic #3470 Slice 2e / AC-5). Exposing the focal lets the picker pre-fill the
/// saved value instead of always starting at 0.5/0.5.
/// </summary>
public sealed record CoverContextAssignmentDto(
    CoverAssignmentSource Source,
    double FocalX,
    double FocalY);

/// <summary>
/// Which cover assignment is currently pinned for each UI context. A <see langword="null"/>
/// field means the context has no explicit admin assignment and falls back to the
/// resolver's implicit precedence.
/// </summary>
public sealed record CoverContextAssignmentsDto(
    CoverContextAssignmentDto? Card,
    CoverContextAssignmentDto? Hero,
    CoverContextAssignmentDto? Social);
