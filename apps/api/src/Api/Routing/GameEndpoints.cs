using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Game management endpoints.
/// Handles game CRUD operations and listing.
/// </summary>
public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder group)
    {
        // Get all games (DDD/CQRS)
        group.MapGet("/games", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetAllGamesQuery();
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get game by ID (DDD/CQRS)
        group.MapGet("/games/{id}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetGameByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get all sessions for a game (DDD/CQRS)
        // Issue #1675: Frontend needs game sessions listing
        group.MapGet("/games/{id}/sessions", async (
            Guid id,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetGameSessionsQuery(id, pageNumber, pageSize);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get game details with extended metadata and statistics (DDD/CQRS)
        // Issue #1196: Supports Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/details", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetGameDetailsQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get rule specifications for a game (DDD/CQRS)
        // Issue #1196: Supports Rules tab in Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/rules", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetRuleSpecsQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Create game (DDD/CQRS) - Admin/Editor only
        group.MapPost("/games", async (
            Api.BoundedContexts.GameManagement.Application.DTOs.CreateGameRequest request,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Auth check
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var command = new CreateGameCommand(
                Title: request.Title,
                Publisher: request.Publisher,
                YearPublished: request.YearPublished,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                MinPlayTimeMinutes: request.MinPlayTimeMinutes,
                MaxPlayTimeMinutes: request.MaxPlayTimeMinutes
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Created game {GameId} via DDD/CQRS", result.Id);
            return Results.Created($"/api/v1/games/{result.Id}", result);
        });

        // Update game (DDD/CQRS) - Admin/Editor only
        group.MapPut("/games/{id}", async (
            Guid id,
            Api.BoundedContexts.GameManagement.Application.DTOs.UpdateGameRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var command = new UpdateGameCommand(
                GameId: id,
                Title: request.Title,
                Publisher: request.Publisher,
                YearPublished: request.YearPublished,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                MinPlayTimeMinutes: request.MinPlayTimeMinutes,
                MaxPlayTimeMinutes: request.MaxPlayTimeMinutes
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        });

        // ========================================
        // GameSession CQRS Endpoints
        // ========================================

        // Start game session
        group.MapPost("/sessions", async (
            StartGameSessionRequest request,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new StartGameSessionCommand(
                GameId: request.GameId,
                Players: request.Players
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Started game session {SessionId} for game {GameId}", result.Id, result.GameId);
            return Results.Created($"/api/v1/sessions/{result.Id}", result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Add player to session
        group.MapPost("/sessions/{id}/players", async (
            Guid id,
            SessionPlayerRequest request,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new AddPlayerToSessionCommand(
                SessionId: id,
                PlayerName: request.PlayerName,
                PlayerOrder: request.PlayerOrder,
                Color: request.Color
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Added player {PlayerName} to session {SessionId}", request.PlayerName, id);
            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Complete game session
        group.MapPost("/sessions/{id}/complete", async (
            Guid id,
            CompleteSessionRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new CompleteGameSessionCommand(
                SessionId: id,
                WinnerName: request?.WinnerName
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Abandon game session
        group.MapPost("/sessions/{id}/abandon", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new AbandonGameSessionCommand(
                SessionId: id,
                Reason: null
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Get session by ID
        group.MapGet("/sessions/{id}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetGameSessionByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get active sessions for game
        group.MapGet("/games/{gameId}/sessions/active", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetActiveSessionsByGameQuery(gameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Pause game session
        group.MapPost("/sessions/{id}/pause", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new PauseGameSessionCommand(SessionId: id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Resume game session
        group.MapPost("/sessions/{id}/resume", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new ResumeGameSessionCommand(SessionId: id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // End game session (alias for complete)
        group.MapPost("/sessions/{id}/end", async (
            Guid id,
            CompleteSessionRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter

            var command = new EndGameSessionCommand(
                SessionId: id,
                WinnerName: request?.WinnerName
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // Get all active sessions (with pagination)
        group.MapGet("/sessions/active", async (
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetActiveSessionsQuery(Limit: limit, Offset: offset);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get session history (with filters and pagination)
        group.MapGet("/sessions/history", async (
            [FromQuery] Guid? gameId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetSessionHistoryQuery(
                GameId: gameId,
                StartDate: startDate,
                EndDate: endDate,
                Limit: limit,
                Offset: offset
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get session statistics (aggregated stats with filters)
        group.MapGet("/sessions/statistics", async (
            [FromQuery] Guid? gameId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int topPlayersLimit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            // Default topPlayersLimit to 5 if not specified or invalid, cap at 100 for performance
            if (topPlayersLimit <= 0)
                topPlayersLimit = 5;
            if (topPlayersLimit > 100)
                topPlayersLimit = 100;

            var query = new GetSessionStatsQuery(
                GameId: gameId,
                StartDate: startDate,
                EndDate: endDate,
                TopPlayersLimit: topPlayersLimit
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // ========================================
        // GameFAQ CQRS Endpoints (Issue #2028)
        // ========================================

        // Get FAQs for a game (public, paginated)
        group.MapGet("/games/{gameId}/faqs", async (
            Guid gameId,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var query = new GetGameFAQsQuery(
                GameId: gameId,
                Limit: limit ?? 10,
                Offset: offset ?? 0
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Create FAQ for a game (admin/editor only)
        group.MapPost("/games/{gameId}/faqs", async (
            Guid gameId,
            CreateGameFAQRequest request,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Auth check
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var command = new CreateGameFAQCommand(
                GameId: gameId,
                Question: request.Question,
                Answer: request.Answer
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Created FAQ {FAQId} for game {GameId}", result.Id, gameId);
            return Results.Created($"/api/v1/faqs/{result.Id}", result);
        });

        // Update FAQ (admin/editor only)
        group.MapPut("/faqs/{id}", async (
            Guid id,
            UpdateGameFAQRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var command = new UpdateGameFAQCommand(
                Id: id,
                Question: request.Question,
                Answer: request.Answer
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        });

        // Delete FAQ (admin/editor only)
        group.MapDelete("/faqs/{id}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var command = new DeleteGameFAQCommand(Id: id);
            await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        // Upvote FAQ (public)
        group.MapPost("/faqs/{id}/upvote", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)

            var command = new UpvoteGameFAQCommand(Id: id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        return group;
    }
}
