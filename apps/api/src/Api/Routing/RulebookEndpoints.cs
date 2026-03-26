using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Rulebook upload and knowledge base retrieval endpoints.
/// POST /games/{gameId}/rulebook — Upload a rulebook PDF with deduplication.
/// GET /users/{userId}/games/with-kb — List games that have knowledge base documents.
/// </summary>
internal static class RulebookEndpoints
{
    public static RouteGroupBuilder MapRulebookEndpoints(this RouteGroupBuilder group)
    {
        MapUploadRulebookEndpoint(group);
        MapGetGamesWithKbEndpoint(group);

        return group;
    }

    private static void MapUploadRulebookEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/games/{gameId:guid}/rulebook", HandleUploadRulebook)
             .DisableAntiforgery()
             .WithMetadata(new RequestSizeLimitAttribute(104_857_600)) // 100 MB
             .RequireAuthorization()
             .WithTags("Rulebook")
             .WithName("UploadRulebook")
             .WithOpenApi(operation =>
             {
                 operation.Summary = "Upload a rulebook PDF for a game";
                 operation.Description = "Uploads a PDF rulebook with content-hash deduplication. If an identical PDF already exists and is non-failed, reuses it by creating an EntityLink.";
                 return operation;
             });
    }

    private static void MapGetGamesWithKbEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/{userId:guid}/games/with-kb", HandleGetGamesWithKb)
             .RequireAuthorization()
             .WithTags("Rulebook")
             .WithName("GetGamesWithKb")
             .WithOpenApi(operation =>
             {
                 operation.Summary = "List games with knowledge base documents";
                 operation.Description = "Returns all games that have at least one uploaded PDF for the authenticated user.";
                 return operation;
             });
    }

    private static async Task<IResult> HandleUploadRulebook(
        Guid gameId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var file = httpContext.Request.Form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "A PDF file is required." });
        }

        var command = new AddRulebookCommand(gameId, userId, file);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGamesWithKb(
        Guid userId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var authenticatedUserId = httpContext.User.GetUserId();

        if (authenticatedUserId != userId)
        {
            return Results.Forbid();
        }

        var query = new GetGamesWithKbQuery(userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }
}
