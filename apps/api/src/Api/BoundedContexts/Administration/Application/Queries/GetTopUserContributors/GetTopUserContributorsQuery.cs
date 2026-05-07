using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.GetTopUserContributors;

/// <summary>
/// Query for the SP4 /discover "Top contributors" rail
/// (Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805).
/// </summary>
/// <param name="Limit">Number of contributors to return. Validator clamps to [1, 50]; default 10.</param>
/// <remarks>
/// Distinct surface from the existing public
/// <c>/shared-games/top-contributors</c> leaderboard
/// (<see cref="SharedGameCatalog.Application.Queries.GetTopContributors.GetTopContributorsQuery"/>),
/// which scores by <c>sessions + wins * 2</c>. This query aggregates
/// <em>contribution sources</em> (FAQs authored, KB documents uploaded,
/// AI agents created) to surface community editors rather than active players.
/// </remarks>
internal sealed record GetTopUserContributorsQuery(int Limit = 10)
    : IQuery<TopUserContributorsResponse>;
