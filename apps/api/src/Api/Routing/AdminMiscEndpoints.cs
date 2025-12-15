using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
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
internal static class AdminMiscEndpoints
{
    public static RouteGroupBuilder MapAdminMiscEndpoints(this RouteGroupBuilder group)
    {
        // CHESS-03: Chess knowledge indexing endpoints
        group.MapPost("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;
            var session = sessionResult.Session;
            if (session == null) throw new InvalidOperationException("Session is required");

            logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session!.User!.Id);

            var result = await mediator.Send(new IndexChessKnowledgeCommand(), ct).ConfigureAwait(false);

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
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Issue #1445: Use centralized query validation
            var queryError = QueryValidator.ValidateQuery(q);
            if (queryError != null)
            {
                return Results.BadRequest(new { error = queryError });
            }
            var validatedQuery = q!;

            logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session!.User!.Id, validatedQuery);

            var searchResult = await mediator.Send(new SearchChessKnowledgeQuery { Query = validatedQuery, Limit = limit ?? 5 }, ct).ConfigureAwait(false);

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
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapDelete("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} deleting all chess knowledge", session!.User!.Id);

            var success = await mediator.Send(new DeleteChessKnowledgeCommand(), ct).ConfigureAwait(false);

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
