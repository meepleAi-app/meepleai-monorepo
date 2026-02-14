using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User library endpoints.
/// Handles user game library management: adding, removing, updating, and viewing games.
/// </summary>
internal static class UserLibraryEndpoints
{
    public static RouteGroupBuilder MapUserLibraryEndpoints(this RouteGroupBuilder group)
    {
        MapGetUserLibraryEndpoint(group);
        MapGetLibraryStatsEndpoint(group);
        MapGetLibraryQuotaEndpoint(group);
        MapAddGameToLibraryEndpoint(group);
        MapRemoveGameFromLibraryEndpoint(group);
        MapUpdateLibraryEntryEndpoint(group);
        MapGetGameInLibraryStatusEndpoint(group);

        // Agent configuration endpoints
        MapGetGameAgentConfigEndpoint(group);
        MapConfigureGameAgentEndpoint(group);
        MapResetGameAgentEndpoint(group);
        MapSaveAgentConfigEndpoint(group); // Issue #3212
        MapCreateGameAgentEndpoint(group); // Issue #5

        // Custom PDF endpoints
        MapUploadCustomGamePdfEndpoint(group);
        MapResetGamePdfEndpoint(group);
        MapGetGamePdfsEndpoint(group); // Issue #3152
        MapPrivatePdfProgressStreamEndpoint(group); // Issue #3653
        MapRemovePrivatePdfEndpoint(group); // Issue #3651

        // Library sharing endpoints
        MapCreateLibraryShareLinkEndpoint(group);
        MapGetLibraryShareLinkEndpoint(group);
        MapUpdateLibraryShareLinkEndpoint(group);
        MapRevokeLibraryShareLinkEndpoint(group);
        MapGetSharedLibraryEndpoint(group);

        // Game detail endpoints (Epic #2823)
        MapGetGameDetailEndpoint(group);
        MapGetGameChecklistEndpoint(group);
        MapUpdateGameStateEndpoint(group);
        MapRecordGameSessionEndpoint(group);
        MapSendLoanReminderEndpoint(group);

        // Label endpoints (Epic #3511)
        MapGetLabelsEndpoint(group);
        MapGetGameLabelsEndpoint(group);
        MapAddLabelToGameEndpoint(group);
        MapRemoveLabelFromGameEndpoint(group);
        MapCreateCustomLabelEndpoint(group);
        MapDeleteCustomLabelEndpoint(group);

        // Generic collection endpoints (Issue #4263)
        MapGetCollectionStatusEndpoint(group);
        MapAddToCollectionEndpoint(group);
        MapRemoveFromCollectionEndpoint(group);

        // Bulk collection endpoints (Issue #4268)
        MapBulkAddToCollectionEndpoint(group);
        MapBulkRemoveFromCollectionEndpoint(group);
        MapBulkGetCollectionAssociatedDataEndpoint(group);

        return group;
    }

    private static void MapGetUserLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool? favoritesOnly,
            [FromQuery] string[]? stateFilter,
            [FromQuery] string? sortBy,
            [FromQuery] bool? sortDescending,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetUserLibraryQuery(
                UserId: userId,
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                FavoritesOnly: favoritesOnly,
                StateFilter: stateFilter,
                SortBy: sortBy ?? "addedAt",
                Descending: sortDescending ?? true
            );
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<PaginatedLibraryResponseDto>(200)
        .WithTags("Library")
        .WithSummary("Get user's game library")
        .WithDescription("Returns paginated list of games in user's library. Supports filtering by favorites, state (Nuovo/InPrestito/Wishlist/Owned), and sorting by addedAt, title, or favorite status.")
        .WithOpenApi();
    }

    private static void MapGetLibraryStatsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/stats", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetLibraryStatsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryStatsDto>(200)
        .WithTags("Library")
        .WithSummary("Get library statistics")
        .WithDescription("Returns statistics about user's library including total games, favorites count, and date range.")
        .WithOpenApi();
    }

    private static void MapGetLibraryQuotaEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/quota", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetLibraryQuotaQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<LibraryQuotaDto>(200)
        .WithTags("Library")
        .WithSummary("Get library quota")
        .WithDescription("Returns quota information for user's library including games in library, max allowed, remaining slots, and tier.")
        .WithOpenApi();
    }

    private static void MapAddGameToLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}", async (
            Guid gameId,
            [FromBody] AddGameToLibraryRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new AddGameToLibraryCommand(
                UserId: userId,
                GameId: gameId,
                Notes: request?.Notes,
                IsFavorite: request?.IsFavorite ?? false
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/games/{gameId}", result);
            }
            catch (DomainException ex) when (ex.Message.Contains("already in"))
            {
                return Results.Conflict(new { error = "Game is already in library" });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryEntryDto>(201)
        .Produces(401)
        .Produces(409)
        .WithTags("Library")
        .WithSummary("Add game to library")
        .WithDescription("Adds a game to user's library with optional notes and favorite status. Returns 409 if game already in library.")
        .WithOpenApi();
    }

    private static void MapRemoveGameFromLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new RemoveGameFromLibraryCommand(userId, gameId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (DomainException ex) when (ex.Message.Contains("not found"))
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Remove game from library")
        .WithDescription("Removes a game from user's library. Returns 404 if game not in library.")
        .WithOpenApi();
    }

    private static void MapUpdateLibraryEntryEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/library/games/{gameId:guid}", async (
            Guid gameId,
            [FromBody] UpdateLibraryEntryRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UpdateLibraryEntryCommand(
                UserId: userId,
                GameId: gameId,
                Notes: request.Notes,
                IsFavorite: request.IsFavorite
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
        .WithSummary("Update library entry")
        .WithDescription("Updates notes and/or favorite status for a game in user's library. Returns 404 if game not in library.")
        .WithOpenApi();
    }

    private static void MapGetGameInLibraryStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/status", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGameInLibraryStatusQuery(userId, gameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<GameInLibraryStatusDto>(200)
        .Produces(401)
        .WithTags("Library")
        .WithSummary("Check if game is in library with associated data")
        .WithDescription("Returns whether a game is in user's library, favorite status, and detailed counts of associated data (agent config, PDFs, chat sessions, game sessions, checklists, labels). Used by collection quick actions to display Add/Remove buttons and pre-removal warnings.")
        .WithOpenApi();
    }

    private static void MapGetGameAgentConfigEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/agent-config", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGameAgentConfigQuery(userId, gameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentConfigDto?>(200)
        .Produces(401)
        .WithTags("Library")
        .WithSummary("Get AI agent configuration")
        .WithDescription("Returns the custom AI agent configuration for a game in user's library. Returns null if no custom configuration exists (defaults should be used).")
        .WithOpenApi();
    }

    private static void MapConfigureGameAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/library/games/{gameId:guid}/agent", async (
            Guid gameId,
            [FromBody] AgentConfigDto agentConfig,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new ConfigureGameAgentCommand(userId, gameId, agentConfig);

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
        .WithSummary("Configure custom AI agent")
        .WithDescription("Configures a custom AI agent for a game in user's library. Replaces any existing configuration.")
        .WithOpenApi();
    }

    private static void MapResetGameAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}/agent", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new ResetGameAgentCommand(userId, gameId);

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
        .WithSummary("Reset AI agent to default")
        .WithDescription("Resets AI agent to system default configuration for a game in user's library.")
        .WithOpenApi();
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

            if (!TryGetUserId(context, session, out var userId))
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

            if (!TryGetUserId(context, session, out var userId))
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

            if (!TryGetUserId(context, session, out var userId))
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

            if (!TryGetUserId(context, session, out var userId))
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

            if (!TryGetUserId(context, session, out var userId))
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

    private static void MapCreateLibraryShareLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/share", async (
            [FromBody] CreateLibraryShareRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new CreateLibraryShareLinkCommand(
                UserId: userId,
                PrivacyLevel: request.PrivacyLevel,
                IncludeNotes: request.IncludeNotes,
                ExpiresAt: request.ExpiresAt
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/share", result);
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<LibraryShareLinkDto>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Library", "Sharing")
        .WithSummary("Create library share link")
        .WithDescription("Creates a new share link for user's library. Revokes any existing active link. Max 10 per day.")
        .WithOpenApi();
    }

    private static void MapGetLibraryShareLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/share", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetLibraryShareLinkQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.Ok((LibraryShareLinkDto?)null);
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<LibraryShareLinkDto?>(200)
        .Produces(401)
        .WithTags("Library", "Sharing")
        .WithSummary("Get active library share link")
        .WithDescription("Returns the user's active library share link, or null if none exists.")
        .WithOpenApi();
    }

    private static void MapUpdateLibraryShareLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/library/share/{shareToken}", async (
            string shareToken,
            [FromBody] UpdateLibraryShareRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UpdateLibraryShareLinkCommand(
                UserId: userId,
                ShareToken: shareToken,
                PrivacyLevel: request.PrivacyLevel,
                IncludeNotes: request.IncludeNotes,
                ExpiresAt: request.ExpiresAt
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<LibraryShareLinkDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Sharing")
        .WithSummary("Update library share link")
        .WithDescription("Updates settings for an existing library share link.")
        .WithOpenApi();
    }

    private static void MapRevokeLibraryShareLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/share/{shareToken}", async (
            string shareToken,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new RevokeLibraryShareLinkCommand(userId, shareToken);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Sharing")
        .WithSummary("Revoke library share link")
        .WithDescription("Revokes (deletes) a library share link. The link will return 404 after revocation.")
        .WithOpenApi();
    }

    private static void MapGetSharedLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/shared/{shareToken}", async (
            string shareToken,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSharedLibraryQuery(shareToken);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Shared library not found or link has expired" });
            }

            return Results.Ok(result);
        })
        .AllowAnonymous()
        .Produces<SharedLibraryDto>(200)
        .Produces(404)
        .WithTags("Library", "Sharing", "Public")
        .WithSummary("Get shared library (public)")
        .WithDescription("Returns a publicly shared library. No authentication required.")
        .WithOpenApi();
    }


    private static void MapGetGameDetailEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGameDetailQuery(userId, gameId);

            try
            {
                var result = await mediator.Send(query, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<GameDetailDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Games")
        .WithSummary("Get game detail")
        .WithDescription("Returns complete game detail with stats, sessions, and checklist for game in user's library.")
        .WithOpenApi();
    }

    private static void MapGetGameChecklistEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/checklist", async (
            Guid gameId,
            [FromQuery] bool includeWizard,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetGameChecklistQuery(gameId, includeWizard);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .Produces<ChecklistDto>(200)
        .WithTags("Library", "Games", "Public")
        .WithSummary("Get game checklist")
        .WithDescription("Returns setup checklist for a game with optional wizard steps. Public endpoint.")
        .WithOpenApi();
    }

    private static void MapUpdateGameStateEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/library/games/{gameId:guid}/state", async (
            Guid gameId,
            [FromBody] UpdateGameStateRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UpdateGameStateCommand(userId, gameId, request.NewState, request.StateNotes);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Games")
        .WithSummary("Update game state")
        .WithDescription("Updates game state (Nuovo/InPrestito/Wishlist/Owned). Returns 409 for invalid transitions.")
        .WithOpenApi();
    }

    private static void MapRecordGameSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/sessions", async (
            Guid gameId,
            [FromBody] RecordGameSessionRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new RecordGameSessionCommand(
                userId,
                gameId,
                request.PlayedAt,
                request.DurationMinutes,
                request.DidWin,
                request.Players,
                request.Notes);

            try
            {
                var sessionId = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/games/{gameId}/sessions/{sessionId}", new { sessionId });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<object>(201)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Games", "Sessions")
        .WithSummary("Record game session")
        .WithDescription("Records a new gameplay session with automatic stats updates.")
        .WithOpenApi();
    }


    private static void MapSendLoanReminderEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/remind-loan", async (
            Guid gameId,
            [FromBody] SendLoanReminderRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new SendLoanReminderCommand(userId, gameId, request?.CustomMessage);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Games", "Notifications")
        .WithSummary("Send loan reminder")
        .WithDescription("Sends notification reminder about loaned game. Rate limited to 1 per 24h. Game must be in InPrestito state.")
        .WithOpenApi();
    }

    #region Label Endpoints (Epic #3511)

    private static void MapGetLabelsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/labels", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetLabelsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<LabelDto>>(200)
        .Produces(401)
        .WithTags("Library", "Labels")
        .WithSummary("Get available labels")
        .WithDescription("Returns all labels available to the user (predefined system labels + user's custom labels).")
        .WithOpenApi();
    }

    private static void MapGetGameLabelsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/labels", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetGameLabelsQuery(userId, gameId);

            try
            {
                var result = await mediator.Send(query, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<LabelDto>>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Labels")
        .WithSummary("Get game labels")
        .WithDescription("Returns labels assigned to a specific game in user's library.")
        .WithOpenApi();
    }

    private static void MapAddLabelToGameEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/labels/{labelId:guid}", async (
            Guid gameId,
            Guid labelId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new AddLabelToGameCommand(userId, gameId, labelId);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<bool>(200)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Add label to game")
        .WithDescription("Assigns a label to a game in user's library. Returns 409 if label is already assigned.")
        .WithOpenApi();
    }

    private static void MapRemoveLabelFromGameEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}/labels/{labelId:guid}", async (
            Guid gameId,
            Guid labelId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new RemoveLabelFromGameCommand(userId, gameId, labelId);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return result ? Results.NoContent() : Results.NotFound(new { error = "Label not assigned to this game" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Labels")
        .WithSummary("Remove label from game")
        .WithDescription("Removes a label from a game in user's library.")
        .WithOpenApi();
    }

    private static void MapCreateCustomLabelEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/labels", async (
            [FromBody] CreateCustomLabelRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new CreateCustomLabelCommand(userId, request.Name, request.Color);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/labels/{result.Id}", result);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<LabelDto>(201)
        .Produces(401)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Create custom label")
        .WithDescription("Creates a new custom label for the user. Returns 409 if label name already exists.")
        .WithOpenApi();
    }

    private static void MapDeleteCustomLabelEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/labels/{labelId:guid}", async (
            Guid labelId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new DeleteCustomLabelCommand(userId, labelId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Delete custom label")
        .WithDescription("Deletes a custom label. Returns 404 if not found or not owned. Returns 409 if trying to delete predefined label.")
        .WithOpenApi();
    }

    #endregion

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Maps POST endpoint for saving simplified agent configuration (Issue #3212).
    /// </summary>
    private static void MapSaveAgentConfigEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/agent-config", async (
            Guid gameId,
            [FromBody] SaveAgentConfigRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new SaveAgentConfigCommand(
                userId,
                gameId,
                request.TypologyId,
                request.ModelName,
                request.CostEstimate
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<SaveAgentConfigResponse>(200)
        .Produces<ProblemDetails>(400)
        .Produces<ProblemDetails>(401)
        .Produces<ProblemDetails>(404)
        .Produces<ProblemDetails>(409)
        .WithName("SaveAgentConfig")
        .WithDescription("Save simplified agent configuration for a game (Issue #3212)")
        .WithOpenApi();
    }

    /// <summary>
    /// Maps POST endpoint for creating game agent with custom typology and strategy (Issue #5).
    /// </summary>
    private static void MapCreateGameAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/agent", async (
            Guid gameId,
            [FromBody] CreateGameAgentRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new CreateGameAgentCommand(
                GameId: gameId,
                TypologyId: request.TypologyId,
                StrategyName: request.StrategyName,
                StrategyParameters: request.StrategyParameters,
                UserId: userId
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<CreateGameAgentResult>(200)
        .Produces<ProblemDetails>(400)
        .Produces<ProblemDetails>(401)
        .Produces<ProblemDetails>(404)
        .Produces<ProblemDetails>(409)
        .WithName("CreateGameAgent")
        .WithDescription("Create agent for game with custom typology and strategy (Issue #5)")
        .WithOpenApi();
    }

    private static void MapGetCollectionStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/collections/{entityType}/{entityId:guid}/status", async (
            string entityType,
            Guid entityId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var query = new GetCollectionStatusQuery(userId, parsedEntityType, entityId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<CollectionStatusDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections")
        .WithSummary("Check collection status")
        .WithDescription("Returns whether an entity is in user's collection with associated data counts. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapAddToCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/{entityId:guid}", async (
            string entityType,
            Guid entityId,
            [FromBody] AddToCollectionRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new AddToCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityId: entityId,
                IsFavorite: request?.IsFavorite ?? false,
                Notes: request?.Notes
            );

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/collections/{entityType}/{entityId}/status",
                    new { message = "Entity added to collection" });
            }
            catch (DomainException ex) when (ex.Message.Contains("already in"))
            {
                return Results.Conflict(new { error = "Entity is already in collection" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Collections")
        .WithSummary("Add entity to collection")
        .WithDescription("Adds an entity to user's collection with optional favorite status and notes. Returns 409 if already in collection. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapRemoveFromCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/collections/{entityType}/{entityId:guid}", async (
            string entityType,
            Guid entityId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new RemoveFromCollectionCommand(userId, parsedEntityType, entityId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Collections")
        .WithSummary("Remove entity from collection")
        .WithDescription("Removes an entity from user's collection. Returns 404 if not in collection. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapBulkAddToCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/bulk-add", async (
            string entityType,
            [FromBody] BulkAddToCollectionRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new BulkAddToCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds,
                IsFavorite: request.IsFavorite,
                Notes: request.Notes
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkOperationResult>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Bulk add entities to collection")
        .WithDescription("Adds multiple entities to user's collection. Uses partial success pattern. Max 50 entities. Issue #4268.")
        .WithOpenApi();
    }

    private static void MapBulkRemoveFromCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/collections/{entityType}/bulk-remove", async (
            string entityType,
            [FromBody] BulkRemoveFromCollectionRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new BulkRemoveFromCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkOperationResult>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Bulk remove entities from collection")
        .WithDescription("Removes multiple entities from user's collection. Uses partial success pattern. Max 50 entities. Issue #4268.")
        .WithOpenApi();
    }

    private static void MapBulkGetCollectionAssociatedDataEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/bulk-associated-data", async (
            string entityType,
            [FromBody] BulkGetAssociatedDataRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var query = new GetBulkCollectionAssociatedDataQuery(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkAssociatedDataDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Get bulk associated data")
        .WithDescription("Returns aggregated counts of associated data for multiple collection entries. Used for bulk removal warnings. Issue #4268.")
        .WithOpenApi();
    }
}

/// <summary>
/// Request body for adding a game to library.
/// </summary>
public record AddGameToLibraryRequest(
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for updating a library entry.
/// </summary>
public record UpdateLibraryEntryRequest(
    string? Notes = null,
    bool? IsFavorite = null
);

/// <summary>
/// Request body for uploading custom PDF rulebook.
/// </summary>
public record UploadCustomPdfRequest(
    string PdfUrl,
    long FileSizeBytes,
    string OriginalFileName
);

/// <summary>
/// Request body for creating a library share link.
/// </summary>
public record CreateLibraryShareRequest(
    string PrivacyLevel,
    bool IncludeNotes = false,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Request body for updating a library share link.
/// </summary>
public record UpdateLibraryShareRequest(
    string? PrivacyLevel = null,
    bool? IncludeNotes = null,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Request body for updating game state.
/// </summary>
public record UpdateGameStateRequest(
    string NewState,
    string? StateNotes = null
);

/// <summary>
/// Request body for recording game session.
/// </summary>
public record RecordGameSessionRequest(
    DateTime PlayedAt,
    int DurationMinutes,
    bool? DidWin = null,
    string? Players = null,
    string? Notes = null
);

/// <summary>
/// Request body for sending loan reminder.
/// </summary>
public record SendLoanReminderRequest(
    string? CustomMessage = null
);

/// <summary>
/// Request body for saving agent configuration (Issue #3212).
/// Simplified version of AgentConfigDto for frontend modal.
/// </summary>
public record SaveAgentConfigRequest(
    Guid TypologyId,
    string ModelName,
    double CostEstimate
);

/// <summary>
/// Request body for creating game agent with custom typology and strategy (Issue #5).
/// </summary>
public record CreateGameAgentRequest(
    Guid TypologyId,
    string StrategyName,
    string? StrategyParameters = null
);

/// <summary>
/// Request body for creating a custom label (Epic #3511).
/// </summary>
public record CreateCustomLabelRequest(
    string Name,
    string Color
);

/// <summary>
/// Request body for adding an entity to collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
public record AddToCollectionRequest(
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for bulk adding entities to collection.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkAddToCollectionRequest(
    IReadOnlyList<Guid> EntityIds,
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for bulk removing entities from collection.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkRemoveFromCollectionRequest(
    IReadOnlyList<Guid> EntityIds
);

/// <summary>
/// Request body for getting bulk associated data.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkGetAssociatedDataRequest(
    IReadOnlyList<Guid> EntityIds
);
