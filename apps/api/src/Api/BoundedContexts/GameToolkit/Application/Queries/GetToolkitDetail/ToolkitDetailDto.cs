namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;

/// <summary>
/// Toolkit detail wire DTO for the marketplace surface
/// (Wave 3 Phase 2, PR #732 §5.3.1 / Issue #805).
/// </summary>
/// <remarks>
/// Powers the SP4 /toolkits/[id] route hero + meta sections via the FE
/// <c>useToolkitDetail</c> hook. The <c>Agent</c> field surfaces a
/// truncated system-prompt preview so the hero can render the agent voice
/// without forcing the FE to call a separate agent endpoint.
///
/// Schema reality v1 carryovers documented inline (Gate B):
/// <list type="bullet">
///   <item><c>Description</c>: GameToolkitEntity has no Description column;
///         derived from <c>Name</c> + author DisplayName until v2 schema lands.</item>
///   <item><c>AuthorAvatarUrl</c>: pulled from UserEntity.AvatarUrl (real).</item>
///   <item><c>CoverImageUrl</c>: no column exists; always null in v1.</item>
///   <item><c>InstallCount</c>, <c>RatingAverage</c>, <c>RatingCount</c>:
///         no installation/rating entities; stub 0/null/0 (Phase 4 will add).</item>
///   <item><c>PublishedAt</c>: derived from <c>UpdatedAt</c> when
///         <c>IsPublished</c> + <c>TemplateStatus == Approved</c>; null otherwise.</item>
///   <item><c>YankedAt</c>: no yank workflow yet; always null in v1.</item>
///   <item><c>CurrentVersion</c>: GameToolkitEntity stores int Version;
///         surfaced as <c>"1.0.{version}"</c> until semver schema lands.</item>
/// </list>
///
/// FE wire shape mirrors PR #732 §5.3.1 TypeScript interface exactly so the
/// <c>useToolkitDetail</c> hook can typecheck against the live envelope.
/// </remarks>
internal sealed record ToolkitDetailDto(
    Guid Id,
    string Name,
    string Description,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string? CoverImageUrl,
    ToolkitAgentSummaryDto Agent,
    int KbDocsCount,
    int ToolsCount,
    int InstallCount,
    decimal? RatingAverage,
    int RatingCount,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? YankedAt,
    string CurrentVersion,
    // ── Issue #1144 — Stage 3 marketplace extension (appended for binary compat) ──
    // License:   SPDX-like string (e.g. "CC BY-SA 4.0"). Nullable; FE hides meta row.
    // GameName:  LEFT JOIN of GameEntity via GameToolkit.GameId. Null when toolkit
    //            has no game or the game is soft-deleted.
    // SizeBytes: UTF-8 byte count of AgentConfig + all tool/template JSON columns.
    //            Nullable per spec §5.5.2 versioning policy; impl always populates.
    string? License = null,
    string? GameName = null,
    long? SizeBytes = null);

/// <summary>
/// Truncated agent summary embedded in <see cref="ToolkitDetailDto"/>.
/// SystemPromptPreview is capped at 500 chars (PR #732 §5.3.1).
/// </summary>
internal sealed record ToolkitAgentSummaryDto(
    Guid Id,
    string Name,
    string SystemPromptPreview);

/// <summary>
/// Per-viewer derived flags exposed alongside the toolkit detail
/// (PR #732 §5.2 Wiegers requirement: server-side variant gating).
/// </summary>
/// <param name="IsOwner">True when caller authored the toolkit.</param>
/// <param name="HasInstalled">True when caller has installed any version.</param>
/// <param name="CanRate">
/// Server-derived: <c>HasInstalled &amp;&amp; !alreadyRated &amp;&amp; !isOwner</c>.
/// In v1 (no rating entity) collapses to <c>HasInstalled &amp;&amp; !isOwner</c>.
/// </param>
internal sealed record ViewerContextDto(
    bool IsOwner,
    bool HasInstalled,
    bool CanRate);

/// <summary>
/// Stable response envelope for the detail endpoint — wraps DTO + viewer
/// context together so the FE atomic snapshot matches the hero render.
/// </summary>
internal sealed record ToolkitDetailResponse(
    ToolkitDetailDto Toolkit,
    ViewerContextDto ViewerContext);
