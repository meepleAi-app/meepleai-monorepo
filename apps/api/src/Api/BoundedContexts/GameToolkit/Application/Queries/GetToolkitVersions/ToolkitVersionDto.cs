namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;

/// <summary>
/// Wire DTO for a single toolkit version row
/// (Wave 3 Phase 2, PR #732 §5.3.2 / Issue #805).
/// </summary>
/// <remarks>
/// Schema reality v1 carryover (Gate B): there is no <c>ToolkitVersion</c>
/// entity yet — GameToolkitEntity stores a single <c>int Version</c> column
/// that increments on Publish(). The handler synthesises a single-row stub
/// list so the FE wire shape can stabilise ahead of the Phase 4 schema work.
/// </remarks>
internal sealed record ToolkitVersionDto(
    string Version,
    DateTime PublishedAt,
    DateTime? YankedAt,
    string Changelog,
    bool IsCurrent);

/// <summary>
/// Response envelope for the versions endpoint — mirrors the empty-state
/// contract from PR #732 §3.4 (200 with empty <c>items</c> rather than 404
/// when the toolkit exists but has no published versions yet).
/// </summary>
internal sealed record ToolkitVersionsResponse(
    IReadOnlyList<ToolkitVersionDto> Items);
