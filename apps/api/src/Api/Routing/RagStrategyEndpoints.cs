using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Public endpoints for RAG strategy information.
/// Issue #8: User/Editor wizard strategy selection.
/// </summary>
internal static class RagStrategyEndpoints
{
    public static RouteGroupBuilder MapRagStrategyEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/rag/strategies - List all available strategies (public)
        group.MapGet("/strategies", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Require authentication (any authenticated user can access)
            var (authorized, _, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var query = new GetPublicRagStrategiesQuery();
            var strategies = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new GetRagStrategiesResponse { Strategies = strategies });
        })
        .WithName("GetPublicRagStrategies")
        .WithTags("RAG", "Public")
        .WithSummary("List available RAG strategies")
        .WithDescription(@"
Returns all available RAG strategies for user/editor selection in wizards.

Strategies ordered by complexity (0-11):
- **FAST** (0): Quick lookups, ~1,500 tokens
- **BALANCED** (1): Standard queries, ~2,800 tokens
- **SENTENCE_WINDOW** (5): Precise citations, ~3,250 tokens (+7% accuracy)
- **ITERATIVE** (6): Deep research, ~4,500 tokens (+14% accuracy)
- **MULTI_AGENT** (8): Complex strategic queries, ~12,900 tokens (+20% accuracy)
- **RAG_FUSION** (11): Multiple perspectives, ~11,550 tokens (+11% accuracy)

**Custom strategy (7)** requires Admin privileges (not returned for regular users).")
        .Produces<GetRagStrategiesResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }
}

/// <summary>
/// Response wrapper for GET /api/v1/rag/strategies
/// </summary>
public record GetRagStrategiesResponse
{
    public required List<RagStrategyDto> Strategies { get; init; }
}
