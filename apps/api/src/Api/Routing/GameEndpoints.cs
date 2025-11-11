using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Game management endpoints.
/// Handles game CRUD operations and listing.
/// </summary>
public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder group)
    {
        // Get all games
        group.MapGet("/games", async (HttpContext context, GameService gameService, CancellationToken ct) =>
        {
            // Support both cookie-based session auth and API key auth
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var games = await gameService.GetGamesAsync(ct);
            var response = games.Select(g => new GameResponse(g.Id.ToString(), g.Name, g.CreatedAt)).ToList();
            return Results.Json(response);
        });

        // Create a new game (Admin/Editor only) - Legacy endpoint
        group.MapPost("/games", async (Api.Models.CreateGameRequest? request, HttpContext context, GameService gameService, ILogger<Program> logger, CancellationToken ct) =>
        {
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "User {UserId} with role {Role} attempted to create a game without permission",
                    session.User.Id,
                    session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "Request body is required" });
            }

            try
            {
                var game = await gameService.CreateGameAsync(request.Name, request.GameId, ct);
                logger.LogInformation("Created game {GameId}", game.Id);
                return Results.Created($"/games/{game.Id}", new GameResponse(game.Id.ToString(), game.Name, game.CreatedAt));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid game creation request");
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Conflict creating game");
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // CHAT-06: Get agents for a specific game
        group.MapGet("/games/{gameId}/agents", async (string gameId, HttpContext context, ChatService chatService, CancellationToken ct) =>
        {
            // Support both cookie-based session auth and API key auth
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var agents = await chatService.GetAgentsForGameAsync(gameId, ct);
            var response = agents.Select(a => new AgentDto(
                a.Id.ToString(),
                a.GameId.ToString(),
                a.Name,
                a.Kind,
                a.CreatedAt
            )).ToList();

            return Results.Json(response);
        });

        // ========================================
        // DDD-PHASE2: GameManagement CQRS Endpoints
        // ========================================

        // Create game (CQRS)
        group.MapPost("/games/ddd", async (
            Api.BoundedContexts.GameManagement.Application.DTOs.CreateGameRequest request,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Auth check
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }

            var command = new CreateGameCommand(
                Title: request.Title,
                Publisher: request.Publisher,
                YearPublished: request.YearPublished,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                MinPlayTimeMinutes: request.MinPlayTimeMinutes,
                MaxPlayTimeMinutes: request.MaxPlayTimeMinutes
            );

            var result = await mediator.Send(command, ct);
            logger.LogInformation("Created game {GameId} via CQRS", result.Id);
            return Results.Created($"/api/v1/games/{result.Id}", result);
        });

        // Update game (CQRS)
        group.MapPut("/games/{id}/ddd", async (
            Guid id,
            Api.BoundedContexts.GameManagement.Application.DTOs.UpdateGameRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }

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

            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // Get game by ID (CQRS)
        group.MapGet("/games/{id}/ddd", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var query = new GetGameByIdQuery(id);
            var result = await mediator.Send(query, ct);

            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // Get all games (CQRS)
        group.MapGet("/games/ddd", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var query = new GetAllGamesQuery();
            var result = await mediator.Send(query, ct);

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
            // Auth check
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
            {
                return Results.Unauthorized();
            }

            var command = new StartGameSessionCommand(
                GameId: request.GameId,
                Players: request.Players
            );

            var result = await mediator.Send(command, ct);
            logger.LogInformation("Started game session {SessionId} for game {GameId}", result.Id, result.GameId);
            return Results.Created($"/api/v1/sessions/{result.Id}", result);
        });

        // Complete game session
        group.MapPost("/sessions/{id}/complete", async (
            Guid id,
            CompleteSessionRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
            {
                return Results.Unauthorized();
            }

            var command = new CompleteGameSessionCommand(
                SessionId: id,
                WinnerName: request?.WinnerName
            );

            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // Abandon game session
        group.MapPost("/sessions/{id}/abandon", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
            {
                return Results.Unauthorized();
            }

            var command = new AbandonGameSessionCommand(
                SessionId: id,
                Reason: null
            );

            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // Get session by ID
        group.MapGet("/sessions/{id}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var query = new GetGameSessionByIdQuery(id);
            var result = await mediator.Send(query, ct);

            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // Get active sessions for game
        group.MapGet("/games/{gameId}/sessions/active", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Auth check
            var hasSession = context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession;
            var hasApiKey = context.User.Identity?.IsAuthenticated == true;

            if (!hasSession && !hasApiKey)
            {
                return Results.Unauthorized();
            }

            var query = new GetActiveSessionsByGameQuery(gameId);
            var result = await mediator.Send(query, ct);

            return Results.Ok(result);
        });

        return group;
    }
}
