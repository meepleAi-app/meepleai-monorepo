using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// UserLibrary PDF endpoints: custom PDF upload, listing, and indexing status.
/// </summary>
internal static class UserLibraryPdfEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapUploadCustomGamePdfEndpoint(group);
        MapGetGamePdfsEndpoint(group);
        MapGetGamePdfIndexingStatusEndpoint(group);
    }

    private static void MapUploadCustomGamePdfEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/pdf", async (
            Guid gameId,
            [FromBody] UploadCustomPdfRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UploadCustomGamePdfCommand(
                userId,
                gameId,
                request.PdfUrl,
                request.FileSizeBytes,
                request.OriginalFileName
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (DomainException ex) when (ex.Message.Contains("not found"))
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryEntryDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Upload custom PDF")
        .WithDescription("Uploads a custom PDF rulebook for a game in user's library. Overrides the SharedGame's default PDF.")
        .WithOpenApi();
    }

    private static void MapGetGamePdfsEndpoint(RouteGroupBuilder group)
    {
        // Issue #3152: Get all PDFs for a game (custom + catalog)
        group.MapGet("/library/games/{gameId:guid}/pdfs", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGamePdfsQuery(gameId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<List<GamePdfDto>>(200)
        .Produces(401)
        .WithTags("Library", "PDF")
        .WithSummary("Get game PDFs")
        .WithDescription("Returns all PDFs associated with a game in user's library (custom uploads + shared catalog). Issue #3152.")
        .WithOpenApi();
    }

    /// <summary>
    /// Issue #4943: Returns the PDF indexing/processing status for a game owned by the authenticated user.
    /// Enables frontend polling every 3s until status = indexed | failed.
    /// </summary>
    private static void MapGetGamePdfIndexingStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/pdf-status", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGamePdfIndexingStatusQuery(gameId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<PdfIndexingStatusDto>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Library", "PDF")
        .WithSummary("Get PDF indexing status")
        .WithDescription("Returns the PDF processing/indexing status for a game. Poll every 3s until status=indexed|failed. Issue #4943.")
        .WithOpenApi();
    }
}
