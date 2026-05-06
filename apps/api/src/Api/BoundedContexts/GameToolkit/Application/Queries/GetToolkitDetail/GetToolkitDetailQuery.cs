using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;

/// <summary>
/// Query for the marketplace toolkit detail surface
/// (Wave 3 Phase 2, PR #732 §5.3.1 / Issue #805).
/// </summary>
/// <param name="ToolkitId">Toolkit aggregate id.</param>
/// <param name="ViewerId">
/// Authenticated caller — drives <c>ViewerContext.IsOwner</c> /
/// <c>HasInstalled</c> / <c>CanRate</c> derivation. Required for
/// per-viewer cache key partitioning (§5.3.1: cache 10min + ETag per viewer).
/// </param>
/// <remarks>
/// Returns null when the toolkit is not visible to the viewer per PR #732
/// §5.2 security boundary (unpublished or yanked AND viewer != author);
/// the endpoint translates null to 404. Owners always see their own
/// drafts/yanked toolkits — soft-delete preserves owner access.
/// </remarks>
internal sealed record GetToolkitDetailQuery(
    Guid ToolkitId,
    Guid ViewerId) : IQuery<ToolkitDetailResponse?>;
