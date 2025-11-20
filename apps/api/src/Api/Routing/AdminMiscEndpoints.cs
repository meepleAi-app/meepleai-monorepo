using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Helpers;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Miscellaneous admin endpoints.
/// Handles seed data creation, chess knowledge indexing, and other administrative utilities.
/// </summary>
public static class AdminMiscEndpoints
{
    public static RouteGroupBuilder MapAdminMiscEndpoints(this RouteGroupBuilder group)
    {
        // EDIT-07: Bulk RuleSpec operations
        group.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(request.gameId))
            {
                return Results.BadRequest(new { error = "gameId is required" });
            }

            if (!Guid.TryParse(request.gameId, out var gameGuid))
            {
                return Results.BadRequest(new { error = "Invalid game ID format" });
            }

            logger.LogInformation("Admin {UserId} creating demo RuleSpec for game {GameId}", session!.User.Id, gameGuid);
            var command = new CreateDemoRuleSpecCommand(gameGuid);
            var specDto = await mediator.Send(command, ct);

            // Convert DTO to Model for backward compatibility
            var atoms = specDto.Atoms.Select(a => new RuleAtom(a.Id, a.Text, a.Section, a.Page, a.Line)).ToList();
            var spec = new RuleSpec(specDto.GameId.ToString(), specDto.Version, specDto.CreatedAt, atoms);

            return Results.Json(new { ok = true, spec });
        });

        // CHESS-03: Chess knowledge indexing endpoints
        group.MapPost("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

            var result = await mediator.Send(new IndexChessKnowledgeCommand(), ct);

            if (!result.Success)
            {
                logger.LogError("Chess knowledge indexing failed: {Error}", result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
                result.TotalKnowledgeItems, result.TotalChunks);

            return Results.Json(new
            {
                success = true,
                totalItems = result.TotalKnowledgeItems,
                totalChunks = result.TotalChunks,
                categoryCounts = result.CategoryCounts
            });
        });

        group.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Issue #1445: Use centralized query validation
            var queryError = QueryValidator.ValidateQuery(q);
            if (queryError != null)
            {
                return Results.BadRequest(new { error = queryError });
            }

            logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

            var searchResult = await mediator.Send(new SearchChessKnowledgeQuery { Query = q, Limit = limit ?? 5 }, ct);

            if (!searchResult.Success)
            {
                logger.LogError("Chess knowledge search failed: {Error}", searchResult.ErrorMessage);
                return Results.BadRequest(new { error = searchResult.ErrorMessage });
            }

            logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);

            return Results.Json(new
            {
                success = true,
                results = searchResult.Results.Select(r => new
                {
                    score = r.Score,
                    text = r.Text,
                    page = r.Page,
                    chunkIndex = r.ChunkIndex
                })
            });
        });

        group.MapDelete("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

            var success = await mediator.Send(new DeleteChessKnowledgeCommand(), ct);

            if (!success)
            {
                logger.LogError("Chess knowledge deletion failed");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            logger.LogInformation("Chess knowledge deletion completed successfully");
            return Results.Json(new { success = true });
        });

        return group;
    }
}
