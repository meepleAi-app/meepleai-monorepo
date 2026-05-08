using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitRatings;

/// <summary>
/// Query for the toolkit ratings list with 5-star breakdown
/// (Wave 3 Phase 4b, PR #732 §5.3.3 / Issue #805).
/// </summary>
/// <param name="ToolkitId">Target toolkit aggregate id.</param>
/// <param name="ViewerId">Authenticated viewer id (drives the visibility check).</param>
/// <param name="Cursor">Opaque pagination cursor (null = first page).</param>
/// <param name="Limit">Page size. Validator clamps to [1, 50]; default 20.</param>
/// <remarks>
/// Powers the SP4 <c>/toolkits/[id]</c> ratings tab. Schema reality v1 carryover
/// (Gate B): the <c>ToolkitRating</c> entity does not exist yet — handler returns
/// an empty stub with zeroed breakdown once toolkit existence is verified. The
/// wire shape is stable so the FE can render today and adopt real data without a
/// fetch shape change. Returns <c>null</c> when toolkit is not visible to the
/// viewer (mapped to 404 at the endpoint layer per PR #732 §5.2 security
/// boundary — non-authors must not learn about drafts/yanked toolkits).
/// </remarks>
internal sealed record GetToolkitRatingsQuery(
    Guid ToolkitId,
    Guid ViewerId,
    string? Cursor,
    int Limit = 20
) : IQuery<ToolkitRatingsResponse?>;
