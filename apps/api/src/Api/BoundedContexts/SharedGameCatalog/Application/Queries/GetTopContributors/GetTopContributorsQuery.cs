using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;

/// <summary>
/// Public query for the top global contributors leaderboard surfaced by the
/// `/shared-games` sidebar widget (mockup `sp3-shared-games.jsx`).
/// Issue #593 (Wave A.3a). See spec §5.4.
///
/// <see cref="Limit"/> validated 1..20 by
/// <see cref="GetTopContributorsQueryValidator"/>; default 5 matches the mockup.
/// </summary>
internal sealed record GetTopContributorsQuery(
    int Limit = 5
) : IQuery<List<TopContributorDto>>;
