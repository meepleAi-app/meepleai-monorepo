using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

/// <summary>
/// Query to retrieve the top contributors for the Discover dashboard.
/// Ranking formula: kbUploads + distinct agent definition sessions per user.
/// Issue #728.
/// </summary>
/// <param name="Limit">Maximum number of contributors to return (clamped to 1–20).</param>
internal sealed record GetTopContributorsQuery(int Limit) : IQuery<IReadOnlyList<TopContributorDto>>;
