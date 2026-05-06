using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Query to retrieve the most recently published toolkits for the Discover dashboard.
/// Ranked by CreatedAt DESC (freshness) since GameToolkitEntity has no install-count field.
/// Limit is clamped to [1, 20].
/// Issue #728.
/// </summary>
internal sealed record GetRecommendedToolkitsQuery(int Limit) : IQuery<IReadOnlyList<RecommendedToolkitDto>>;
