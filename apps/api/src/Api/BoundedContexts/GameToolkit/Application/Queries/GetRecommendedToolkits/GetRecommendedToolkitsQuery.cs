using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Query for the recommended toolkits surface
/// (Wave 3 Phase 4a, PR #732 §4.3.4 / Issue #805).
/// </summary>
/// <param name="Limit">Number of toolkits to return. Validator clamps to [1, 50]; default 10.</param>
/// <remarks>
/// Per PR #732 §5.1 (Newman BC decomposition), this query lives in the
/// <c>GameToolkit</c> BC because the marketplace surface extends the existing
/// toolkit aggregate rather than introducing a new <c>MarketplaceBC</c>.
/// Visibility filter: only published toolkits
/// (<c>IsPublished == true &amp;&amp; TemplateStatus == Approved</c>).
/// </remarks>
internal sealed record GetRecommendedToolkitsQuery(int Limit = 10)
    : IQuery<RecommendedToolkitsResponse>;
