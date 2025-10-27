using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;

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
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
            {
                return Results.Unauthorized();
            }

            var games = await gameService.GetGamesAsync(ct);
            var response = games.Select(g => new GameResponse(g.Id, g.Name, g.CreatedAt)).ToList();
            return Results.Json(response);
        });

        // Create a new game (Admin/Editor only)
        group.MapPost("/games", async (CreateGameRequest? request, HttpContext context, GameService gameService, ILogger<Program> logger, CancellationToken ct) =>
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
                return Results.Created($"/games/{game.Id}", new GameResponse(game.Id, game.Name, game.CreatedAt));
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
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
            {
                return Results.Unauthorized();
            }

            var agents = await chatService.GetAgentsForGameAsync(gameId, ct);
            var response = agents.Select(a => new AgentDto(
                a.Id,
                a.GameId,
                a.Name,
                a.Kind,
                a.CreatedAt
            )).ToList();

            return Results.Json(response);
        });

        return group;
    }
}
