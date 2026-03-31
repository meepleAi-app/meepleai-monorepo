using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Core UserLibrary endpoints: CRUD, Agent Configuration, Game Detail &amp; Session,
/// Library Sharing, Toolkit Dashboard, and Ownership Declaration.
/// </summary>
internal static class UserLibraryCoreEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapGetUserLibraryEndpoint(group);
        MapGetLibraryStatsEndpoint(group);
        MapGetLibraryQuotaEndpoint(group);
        MapAddGameToLibraryEndpoint(group);
        MapRemoveGameFromLibraryEndpoint(group);
        MapUpdateLibraryEntryEndpoint(group);
        MapGetGameInLibraryStatusEndpoint(group);
        MapBatchCheckGamesInLibraryEndpoint(group);

        // Agent configuration endpoints
        MapGetGameAgentConfigEndpoint(group);
        MapConfigureGameAgentEndpoint(group);
        MapResetGameAgentEndpoint(group);
        MapSaveAgentConfigEndpoint(group);
        MapCreateGameAgentEndpoint(group);

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

        // Toolkit dashboard endpoints (Issue #5147 — Epic B4)
        MapGetActiveToolkitEndpoint(group);
        MapOverrideToolkitEndpoint(group);
        MapUpdateToolkitWidgetEndpoint(group);

        // Ownership declaration endpoint (Ownership/RAG access feature)
        MapDeclareOwnershipEndpoint(group);
    }

    internal static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
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

    private static void MapGetUserLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
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
                Search: search,
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

    private static void MapBatchCheckGamesInLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/batch-status", async (
            [FromQuery] string gameIds,
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

            // Parse comma-separated game IDs
            List<Guid> parsedGameIds;
            try
            {
                parsedGameIds = gameIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Guid.Parse)
                    .ToList();
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = "Invalid game ID format" });
            }

            var query = new BatchCheckGamesInLibraryQuery(userId, parsedGameIds);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BatchGameLibraryStatusDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Library")
        .WithSummary("Batch check library status for multiple games")
        .WithDescription("Checks library status for multiple games in a single request. Accepts comma-separated game IDs (max 100). Returns dictionary keyed by game ID. Optimized for game grid rendering to eliminate N+1 API calls.")
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
                request.AgentDefinitionId,
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
                AgentDefinitionId: request.AgentDefinitionId,
                StrategyName: request.StrategyName,
                StrategyParameters: request.StrategyParameters,
                UserId: userId,
                UserTier: session?.User?.Tier ?? "Free",
                UserRole: session?.User?.Role ?? "User"
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

    /// <summary>
    /// GET active Toolkit for a game (default or user override).
    /// Returns null (204) when no toolkit has been created yet.
    /// Issue #5147 — Epic B4.
    /// </summary>
    private static void MapGetActiveToolkitEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/toolkit", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var result = await mediator
                .Send(new GetActiveToolkitQuery(gameId, userId), ct)
                .ConfigureAwait(false);

            return result is null ? Results.NoContent() : Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<ToolkitDashboardDto>(200)
        .Produces(204)
        .Produces<ProblemDetails>(401)
        .WithTags("Toolkit")
        .WithSummary("Get active toolkit for a game")
        .WithDescription("Returns the user-specific toolkit override, or the shared default. Returns 204 when no toolkit exists. Issue #5147.")
        .WithOpenApi();
    }

    /// <summary>
    /// PUT — creates a user override of the default toolkit, or renames an existing override.
    /// Idempotent: safe to call multiple times.
    /// Issue #5147 — Epic B4.
    /// </summary>
    private static void MapOverrideToolkitEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/library/games/{gameId:guid}/toolkit", async (
            Guid gameId,
            [FromBody] OverrideToolkitRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var command = new OverrideToolkitCommand(gameId, userId, request?.DisplayName);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<ToolkitDashboardDto>(200)
        .Produces<ProblemDetails>(401)
        .Produces<ProblemDetails>(404)
        .WithTags("Toolkit")
        .WithSummary("Create or update user toolkit override")
        .WithDescription("Clones the default toolkit into a user-specific override (BR-02). Idempotent — renames if override already exists. Issue #5147.")
        .WithOpenApi();
    }

    /// <summary>
    /// PATCH — enables/disables a widget or updates its config JSON.
    /// Auto-clones the default toolkit if needed (BR-02).
    /// Issue #5147 — Epic B4.
    /// </summary>
    private static void MapUpdateToolkitWidgetEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/library/games/{gameId:guid}/toolkit/widgets/{widgetType}", async (
            Guid gameId,
            string widgetType,
            [FromBody] UpdateWidgetRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            if (!Enum.TryParse<WidgetType>(widgetType, ignoreCase: true, out var parsedWidgetType))
                return Results.BadRequest(new { error = $"Invalid widget type: {widgetType}" });

            var command = new UpdateWidgetCommand(gameId, userId, parsedWidgetType, request.IsEnabled, request.ConfigJson);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<ToolkitDashboardDto>(200)
        .Produces<ProblemDetails>(400)
        .Produces<ProblemDetails>(401)
        .Produces<ProblemDetails>(404)
        .WithTags("Toolkit")
        .WithSummary("Update a toolkit widget")
        .WithDescription("Enables/disables a widget or updates its config JSON. Auto-creates a user override if the active toolkit is the default (BR-02). Issue #5147.")
        .WithOpenApi();
    }

    /// <summary>
    /// Declares explicit ownership of a game in the library, granting RAG access.
    /// Idempotent: if already declared, returns current state without error.
    /// </summary>
    private static void MapDeclareOwnershipEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/{gameId:guid}/declare-ownership", async (
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

            var command = new DeclareOwnershipCommand(userId, gameId);

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
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<DeclareOwnershipResult>(200)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Ownership")
        .WithSummary("Declare ownership of a game")
        .WithDescription("Explicitly declares ownership of a game in the library, granting RAG access to the game's knowledge base. Idempotent.")
        .WithOpenApi();
    }
}
