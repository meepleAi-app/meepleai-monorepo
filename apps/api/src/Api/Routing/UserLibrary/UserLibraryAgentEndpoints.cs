using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// UserLibrary Agent Document endpoints: manage which documents the agent uses for RAG.
/// </summary>
internal static class UserLibraryAgentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapGetAgentDocuments(group);
        MapUpdateAgentDocuments(group);
    }

    private static void MapGetAgentDocuments(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/agent/documents", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await mediator.Send(
                    new GetAvailableDocumentsForGameQuery(gameId, userId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AvailableDocumentsDto>(200)
        .Produces(401)
        .Produces(404)
        .WithName("GetAgentDocumentsForGame")
        .WithTags("Library")
        .WithSummary("Get available documents for agent configuration")
        .WithOpenApi();
    }

    private static void MapUpdateAgentDocuments(RouteGroupBuilder group)
    {
        group.MapPut("/library/games/{gameId:guid}/agent/documents", async (
            Guid gameId,
            [FromBody] UpdateUserAgentDocumentsRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                await mediator.Send(
                    new UpdateUserAgentDocumentsCommand(gameId, userId, request.SelectedDocumentIds), ct)
                    .ConfigureAwait(false);
                return Results.Ok(new { message = "Documenti agente aggiornati" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ForbiddenException)
            {
                return Results.Forbid();
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409)
        .WithName("UpdateAgentDocumentsForGame")
        .WithTags("Library")
        .WithSummary("Update which documents the agent uses for RAG")
        .WithOpenApi();
    }
}

internal record UpdateUserAgentDocumentsRequest(List<Guid> SelectedDocumentIds);
