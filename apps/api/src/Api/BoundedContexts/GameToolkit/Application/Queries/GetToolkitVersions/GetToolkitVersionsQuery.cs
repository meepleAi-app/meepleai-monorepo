using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;

/// <summary>
/// Query for a toolkit's published version history
/// (Wave 3 Phase 2, PR #732 §5.3.2 / Issue #805).
/// </summary>
/// <param name="ToolkitId">Toolkit aggregate id.</param>
/// <param name="ViewerId">
/// Authenticated caller — used by the security boundary (PR #732 §5.2):
/// non-authors can only see versions of published+non-yanked toolkits.
/// </param>
/// <remarks>
/// Returns <c>null</c> from the handler when the toolkit cannot be found
/// or is not visible to the viewer; the endpoint maps that to 404.
/// Returning an envelope with an empty <c>Items</c> list is reserved for
/// the case where the toolkit exists and is visible but has no published
/// versions yet (handler stub creates a single v1.0.x row in v1).
/// </remarks>
internal sealed record GetToolkitVersionsQuery(
    Guid ToolkitId,
    Guid ViewerId) : IQuery<ToolkitVersionsResponse?>;
