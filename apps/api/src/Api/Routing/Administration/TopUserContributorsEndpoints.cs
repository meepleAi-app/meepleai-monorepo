using Api.BoundedContexts.Administration.Application.Queries.GetTopUserContributors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.AdministrationDiscover;

/// <summary>
/// Public discover-surface endpoint that aggregates user contribution sources
/// (Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805).
/// </summary>
/// <remarks>
/// Distinct surface from <c>/api/v1/shared-games/top-contributors</c>
/// (sessions/wins score for the public /shared-games sidebar) — this endpoint
/// powers the SP4 /discover route's "Top contributors" rail with a
/// per-source breakdown (FAQs authored, KB documents uploaded, AI agents
/// created).
/// </remarks>
internal static class TopUserContributorsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users/top-contributors", HandleGetTopUserContributors)
            .RequireAuthorization()
            .WithName("GetTopUserContributors")
            .WithTags("Discover")
            .WithSummary("Get the top user contributors")
            .WithDescription(
                "Returns top users ranked by aggregate contribution count "
                + "(FAQs authored + KB documents uploaded + AI agents created), "
                + "with a per-source breakdown. Limit clamped 1..50, default 10. "
                + "Schema reality v1 carryover (Gate B): only the KB uploads "
                + "metric is real; FAQs + AgentsCreated are stubbed to 0 until "
                + "creator FK columns land. Privacy guards: skips suspended + "
                + "non-Active accounts and users without a DisplayName. Cache: "
                + "1h HybridCache (V2 deferred: materialized view daily refresh). "
                + "Powers the SP4 /discover \"Top contributors\" rail. "
                + "Wave 3 Phase 4a (Issue #805 / PR #732 §4.3.6).")
            .Produces<TopUserContributorsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleGetTopUserContributors(
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Limit clamping at the endpoint layer (PR #732 §3.3 — UI-friendly
        // silent clamp rather than 400). Validator on the query enforces the
        // same range for the CQRS handler.
        var safeLimit = limit.GetValueOrDefault(10);
        if (safeLimit < 1) safeLimit = 10;
        if (safeLimit > 50) safeLimit = 50;

        var query = new GetTopUserContributorsQuery(safeLimit);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(response);
    }
}
