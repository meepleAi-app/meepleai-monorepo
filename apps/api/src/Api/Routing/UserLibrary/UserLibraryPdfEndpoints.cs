using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// UserLibrary PDF endpoints: custom PDF upload/removal, indexing status, and SSE progress stream.
/// </summary>
internal static class UserLibraryPdfEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapUploadCustomGamePdfEndpoint(group);
        MapResetGamePdfEndpoint(group);
        MapGetGamePdfsEndpoint(group);
        MapGetGamePdfIndexingStatusEndpoint(group);
        MapGetPrivateGamePdfIndexingStatusEndpoint(group);
        MapPrivatePdfProgressStreamEndpoint(group);
        MapRemovePrivatePdfEndpoint(group);
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
        .WithSummary("Upload custom PDF rulebook")
        .WithDescription("Uploads a custom PDF rulebook for a game in user's library. Overrides the SharedGame's default PDF.")
        .WithOpenApi();
    }

    private static void MapResetGamePdfEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}/pdf", async (
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

            var command = new ResetGamePdfCommand(userId, gameId);

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
        .WithSummary("Reset to default PDF")
        .WithDescription("Resets to use SharedGame's default PDF rulebook for a game in user's library.")
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

    /// <summary>
    /// Issue #5215: Alias for /library/games/{gameId}/pdf-status under the private-games URL prefix.
    /// Both endpoints accept ONLY private game IDs — authorization is enforced via
    /// IPrivateGameRepository, which only knows about private games. Shared catalog game IDs
    /// will return 404. The /library/ prefix is intentional to match the sibling endpoint
    /// /library/games/{gameId}/pdf-status that this aliases.
    /// </summary>
    private static void MapGetPrivateGamePdfIndexingStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/private-games/{gameId:guid}/pdf-status", async (
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
        .WithTags("Library", "PDF", "PrivateGames")
        .WithSummary("Get PDF indexing status (private game)")
        .WithDescription("Alias for /library/games/{gameId}/pdf-status under private-games URL. Accepts ONLY private game IDs — shared game IDs return 404. Issue #5215.")
        .WithOpenApi();
    }

    /// <summary>
    /// Issue #3653: SSE endpoint for streaming private PDF processing progress.
    /// </summary>
    private static void MapPrivatePdfProgressStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/{entryId:guid}/pdf/progress", async (
            Guid entryId,
            IPrivatePdfProgressStreamService progressService,
            IUserLibraryRepository libraryRepository,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Verify user owns the library entry
            var libraryEntry = await libraryRepository.GetByIdAsync(entryId, ct).ConfigureAwait(false);
            if (libraryEntry is null)
            {
                return Results.NotFound(new { error = "Library entry not found" });
            }

            if (libraryEntry.UserId != userId)
            {
                return Results.Forbid();
            }

            // Set up SSE response
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                await foreach (var progress in progressService.SubscribeToProgress(userId, entryId, ct).ConfigureAwait(false))
                {
                    // Skip heartbeat messages (special percent value of -1)
                    if (progress.Percent == -1)
                    {
                        await context.Response.WriteAsync($": heartbeat\n\n", ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var json = JsonSerializer.Serialize(progress, jsonOptions);
                        await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    }

                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - this is expected
            }

            return Results.Empty;
        })
        .RequireAuthenticatedUser()
        .Produces<ProcessingProgressJson>(200, contentType: "text/event-stream")
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Library", "PDF", "SSE")
        .WithSummary("Stream PDF processing progress (SSE)")
        .WithDescription("Server-Sent Events endpoint for real-time progress updates during private PDF processing. Issue #3653. Heartbeat every 30s.")
        .WithOpenApi();
    }

    /// <summary>
    /// Issue #3651: Remove private PDF from library entry.
    /// Triggers PrivatePdfRemovedEvent to cleanup vectors from private_rules collection.
    /// </summary>
    private static void MapRemovePrivatePdfEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/entries/{entryId:guid}/private-pdf", async (
            Guid entryId,
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

            var command = new RemovePrivatePdfCommand(userId, entryId);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden");
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryEntryDto>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "PDF")
        .WithSummary("Remove private PDF")
        .WithDescription("Removes a private PDF document from a library entry. Triggers cleanup of vectors from private_rules collection. Issue #3651.")
        .WithOpenApi();
    }
}
