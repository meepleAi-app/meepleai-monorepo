using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Game management endpoints.
/// Handles game CRUD operations and listing.
/// </summary>
internal static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder group)
    {
        MapGameRetrievalEndpoints(group);
        MapGameManagementEndpoints(group);
        MapSessionLifecycleEndpoints(group);
        MapSessionQueryEndpoints(group);
        MapSessionStateEndpoints(group); // Issue #2403

        return group;
    }

    private static void MapGameRetrievalEndpoints(RouteGroupBuilder group)
    {
        // Get all games (DDD/CQRS) with pagination support
        // PUBLIC: Allow unauthenticated access for game discovery
        group.MapGet("/games", HandleGetAllGames)
        .AllowAnonymous()
        .Produces<PaginatedGamesResponse>(200)
        .WithTags("Games")
        .WithSummary("Get all games")
        .WithDescription("Returns paginated list of all board games in the catalog. Supports search filtering and pagination. Public endpoint accessible without authentication.");

        // Get game by ID (DDD/CQRS)
        // PUBLIC: Allow unauthenticated access for game details
        group.MapGet("/games/{id}", HandleGetGameById)
        .AllowAnonymous()
        .Produces<GameDto>(200)
        .Produces(404)
        .WithTags("Games")
        .WithSummary("Get game by ID")
        .WithDescription("Returns detailed information for a specific board game. Public endpoint accessible without authentication.");

        // Get all sessions for a game (DDD/CQRS)
        // Issue #1675: Frontend needs game sessions listing
        group.MapGet("/games/{id}/sessions", HandleGetGameSessions)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<List<GameSessionDto>>(200)
        .Produces(401)
        .WithTags("Games", "Sessions")
        .WithSummary("Get all sessions for a game")
        .WithDescription("Returns paginated list of all game sessions for a specific board game. Requires authentication.");

        // Get game details with extended metadata and statistics (DDD/CQRS)
        // Issue #1196: Supports Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/details", HandleGetGameDetails)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<GameDetailsDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Games")
        .WithSummary("Get game details with statistics")
        .WithDescription("Returns extended game information including metadata, statistics, and session history. Supports Game Detail Page. Requires authentication.");

        // Get rule specifications for a game (DDD/CQRS)
        // Issue #1196: Supports Rules tab in Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/rules", HandleGetGameRules)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<IReadOnlyList<RuleSpecDto>>(200)
        .Produces(401)
        .WithTags("Games", "Rules")
        .WithSummary("Get game rule specifications")
        .WithDescription("Returns all rule specification versions for a game. Supports Rules tab in Game Detail Page. Requires authentication.");

        // Get AI agents available for a game
        // Issue #2677: Frontend uses this endpoint for game detail page agent list
        // Note: Agents are game-agnostic, returns all active agents
        group.MapGet("/games/{id}/agents", HandleGetGameAgents)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<List<AgentDto>>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Games", "Agents")
        .WithSummary("Get AI agents for a game")
        .WithDescription("Returns all active AI agents available for the game. Agents are currently game-agnostic. Requires authentication.");

        // Get similar games based on content-based filtering
        // Issue #3353: Similar Games Discovery with RAG
        group.MapGet("/games/{id}/similar", HandleGetSimilarGames)
        .AllowAnonymous() // Public endpoint for game discovery
        .Produces<GetSimilarGamesResult>(200)
        .Produces(404)
        .WithTags("Games", "Discovery")
        .WithSummary("Get similar games")
        .WithDescription("Returns games similar to the specified game based on categories, mechanics, player count, complexity, and duration. Optionally filters out games already owned by the authenticated user.");
    }

    private static void MapGameManagementEndpoints(RouteGroupBuilder group)
    {
        // Create game (DDD/CQRS) - Admin/Editor only
        group.MapPost("/games", HandleCreateGame)
        .Produces<GameDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .WithTags("Games", "Admin")
        .WithSummary("Create a new game")
        .WithDescription("Creates a new board game in the catalog. Admin or Editor role required. Returns created game with generated ID.");

        // Update game (DDD/CQRS) - Admin/Editor only
        group.MapPut("/games/{id}", HandleUpdateGame)
        .Produces<GameDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Games", "Admin")
        .WithSummary("Update an existing game")
        .WithDescription("Updates board game information. Admin or Editor role required. Partial updates supported.");

        // Upload game image (icon or cover) - Issue #2255
        group.MapPost("/games/upload-image", HandleUploadGameImage)
        .DisableAntiforgery() // Required for multipart/form-data file uploads
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .WithTags("Games", "Admin", "Upload")
        .WithSummary("Upload game image")
        .WithDescription("Uploads game icon or cover image. Admin or Editor role required. Accepts multipart/form-data with file, gameId, and imageType fields.");

        // Publish game to SharedGameCatalog - Issue #3481
        group.MapPut("/games/{id}/publish", HandlePublishGame)
        .Produces<GameDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Games", "Admin", "SharedGameCatalog")
        .WithSummary("Publish game to SharedGameCatalog")
        .WithDescription("Updates game publication status and publishes to SharedGameCatalog. Admin role required.");
    }

    private static void MapSessionLifecycleEndpoints(RouteGroupBuilder group)
    {
        // ========================================
        // GameSession CQRS Endpoints
        // ========================================

        // Start game session
        group.MapPost("/sessions", HandleStartSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Sessions")
        .WithSummary("Start a new game session")
        .WithDescription("Creates a new game session with players. Requires active user session.");

        // Add player to session
        group.MapPost("/sessions/{id}/players", HandleAddPlayer)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Add player to session")
        .WithDescription("Adds a new player to an existing game session. Requires active user session.");

        // Complete game session
        group.MapPost("/sessions/{id}/complete", HandleCompleteSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Complete a game session")
        .WithDescription("Marks a game session as completed with optional winner. Requires active user session.");

        // Abandon game session
        group.MapPost("/sessions/{id}/abandon", HandleAbandonSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Abandon a game session")
        .WithDescription("Marks a game session as abandoned. Requires active user session.");

        // Pause game session
        group.MapPost("/sessions/{id}/pause", HandlePauseSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Pause a game session")
        .WithDescription("Pauses an active game session. Requires active user session.");

        // Resume game session
        group.MapPost("/sessions/{id}/resume", HandleResumeSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Resume a paused game session")
        .WithDescription("Resumes a previously paused game session. Requires active user session.");

        // End game session (alias for complete)
        group.MapPost("/sessions/{id}/end", HandleEndSession)
        .RequireSession() // Issue #1446: Automatic session validation
        .Produces<GameSessionDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("End a game session")
        .WithDescription("Ends a game session (alias for complete). Requires active user session.");
    }

    private static void MapSessionQueryEndpoints(RouteGroupBuilder group)
    {
        // Get session by ID
        group.MapGet("/sessions/{id}", HandleGetSessionById)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<GameSessionDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions")
        .WithSummary("Get session by ID")
        .WithDescription("Returns detailed information for a specific game session. Requires authentication.");

        // Get active sessions for game
        group.MapGet("/games/{gameId}/sessions/active", HandleGetActiveSessionsByGame)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<IReadOnlyList<GameSessionDto>>(200)
        .Produces(401)
        .WithTags("Games", "Sessions")
        .WithSummary("Get active sessions for a game")
        .WithDescription("Returns all currently active game sessions for a specific board game. Requires authentication.");

        // Get all active sessions (with pagination)
        group.MapGet("/sessions/active", HandleGetActiveSessions)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<PaginatedSessionsResponseDto>(200)
        .Produces(401)
        .WithTags("Sessions")
        .WithSummary("Get all active sessions")
        .WithDescription("Returns paginated list of all currently active game sessions across all games. Supports limit and offset query parameters. Requires authentication.");

        // Get session history (with filters and pagination)
        group.MapGet("/sessions/history", HandleGetSessionHistory)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<List<GameSessionDto>>(200)
        .Produces(401)
        .WithTags("Sessions")
        .WithSummary("Get session history")
        .WithDescription("Returns paginated list of completed or abandoned game sessions with optional date and game filtering. Requires authentication.");

        // Get session statistics (aggregated stats with filters)
        group.MapGet("/sessions/statistics", HandleGetSessionStats)
        .RequireAuthenticatedUser() // Issue #1446: Dual authentication (session OR API key)
        .Produces<SessionStatsDto>(200)
        .Produces(401)
        .WithTags("Sessions", "Statistics")
        .WithSummary("Get session statistics")
        .WithDescription("Returns aggregated game session statistics with optional filtering by game, date range, and top players limit. Requires authentication.");
    }


    private static async Task<IResult> HandleGetAllGames(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetAllGamesQuery(
            Search: search,
            Page: page ?? 1,
            PageSize: pageSize ?? 20
        );
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameById(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetGameByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleGetGameSessions(
        Guid id,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetGameSessionsQuery(id, pageNumber, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameDetails(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetGameDetailsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleGetGameRules(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetRuleSpecsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    /// <summary>
    /// Get AI agents available for a game.
    /// Issue #2677: Frontend uses this endpoint for game detail page agent list.
    /// Note: Agents are currently game-agnostic, returns all active agents.
    /// </summary>
    private static async Task<IResult> HandleGetGameAgents(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Validate game exists first
        var gameQuery = new GetGameByIdQuery(id);
        var game = await mediator.Send(gameQuery, ct).ConfigureAwait(false);

        if (game == null)
        {
            return Results.NotFound(new { error = $"Game {id} not found" });
        }

        // Get all active system agents (game-agnostic in current design)
        var agentsQuery = new GetAllAgentsQuery(ActiveOnly: true);
        var agents = await mediator.Send(agentsQuery, ct).ConfigureAwait(false);
        var agentList = agents.ToList();

        // Also include user's custom agent if configured for this game.
        // Custom agents are stored in user_library_entries.CustomAgentConfigJson,
        // not in the agents table — so the journey step check (agents.length > 0) would
        // otherwise never see them.
        if (context.Items.TryGetValue(nameof(SessionStatusDto), out var sessionObj) &&
            sessionObj is SessionStatusDto sessionForAgents &&
            sessionForAgents.User != null)
        {
            var agentConfigQuery = new GetGameAgentConfigQuery(sessionForAgents.User.Id, id);
            var agentConfig = await mediator.Send(agentConfigQuery, ct).ConfigureAwait(false);

            if (agentConfig != null)
            {
                // Deterministic synthetic ID: XOR userId and gameId bytes so the same
                // user+game pair always produces the same ID across requests. This prevents
                // React Query from treating each response as a cache miss (agent.id is used
                // as cache key in KnowledgeBaseTab via agentDocumentsKeys.byAgent(agent.id)).
                var userBytes = sessionForAgents.User.Id.ToByteArray();
                var gameBytes = id.ToByteArray();
                var deterministicBytes = new byte[16];
                for (var i = 0; i < 16; i++)
                    deterministicBytes[i] = (byte)(userBytes[i] ^ gameBytes[i]);

                agentList.Add(new AgentDto(
                    Id: new Guid(deterministicBytes),
                    Name: agentConfig.Personality ?? "Custom Agent",
                    Type: "Custom",
                    StrategyName: "custom",
                    StrategyParameters: new Dictionary<string, object>(StringComparer.Ordinal),
                    IsActive: true,
                    CreatedAt: DateTime.UtcNow,
                    LastInvokedAt: null,
                    InvocationCount: 0,
                    IsRecentlyUsed: false,
                    IsIdle: false,
                    GameId: id,
                    CreatedByUserId: sessionForAgents.User.Id));
            }
        }

        return Results.Ok(agentList);
    }

    private static async Task<IResult> HandleCreateGame(
        CreateGameRequest request,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Auth check
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new CreateGameCommand(
            Title: request.Title,
            Publisher: request.Publisher,
            YearPublished: request.YearPublished,
            MinPlayers: request.MinPlayers,
            MaxPlayers: request.MaxPlayers,
            MinPlayTimeMinutes: request.MinPlayTimeMinutes,
            MaxPlayTimeMinutes: request.MaxPlayTimeMinutes,
            IconUrl: request.IconUrl,
            ImageUrl: request.ImageUrl,
            BggId: request.BggId,
            SharedGameId: request.SharedGameId // Issue #2373: Link to SharedGameCatalog
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Created game {GameId} via DDD/CQRS", result.Id);
        return Results.Created($"/api/v1/games/{result.Id}", result);
    }

    private static async Task<IResult> HandleUpdateGame(
        Guid id,
        UpdateGameRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Auth check
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
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
    }

    private static async Task<IResult> HandlePublishGame(
        Guid id,
        PublishGameRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Auth check - Admin only
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new PublishGameCommand(
            GameId: id,
            Status: request.Status
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleStartSession(
        StartGameSessionRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Issue #3070: Get user info for session quota enforcement
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var userId = session!.User!.Id;
        var userTier = UserTier.Parse(session.User!.Tier);
        var userRole = Role.Parse(session.User!.Role);

        var command = new StartGameSessionCommand(
            GameId: request.GameId,
            Players: request.Players,
            UserId: userId,
            UserTier: userTier,
            UserRole: userRole
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Started game session {SessionId} for game {GameId} by user {UserId}", result.Id, result.GameId, userId);
        return Results.Created($"/api/v1/sessions/{result.Id}", result);
    }

    private static async Task<IResult> HandleAddPlayer(
        Guid id,
        SessionPlayerRequest request,
        IMediator mediator,
                ILogger<Program> logger,
        CancellationToken ct)
    {
        var command = new AddPlayerToSessionCommand(
            SessionId: id,
            PlayerName: request.PlayerName,
            PlayerOrder: request.PlayerOrder,
            Color: request.Color
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Added player {PlayerName} to session {SessionId}", request.PlayerName, id);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCompleteSession(
        Guid id,
        CompleteSessionRequest? request,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new CompleteGameSessionCommand(
            SessionId: id,
            WinnerName: request?.WinnerName
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleAbandonSession(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new AbandonGameSessionCommand(
            SessionId: id,
            Reason: null
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandlePauseSession(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new PauseGameSessionCommand(SessionId: id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleResumeSession(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new ResumeGameSessionCommand(SessionId: id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleEndSession(
        Guid id,
        CompleteSessionRequest? request,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new EndGameSessionCommand(
            SessionId: id,
            WinnerName: request?.WinnerName
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionById(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetGameSessionByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleGetActiveSessionsByGame(
        Guid gameId,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetActiveSessionsByGameQuery(gameId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveSessions(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetActiveSessionsQuery(Limit: limit, Offset: offset);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionHistory(
        [FromQuery] Guid? gameId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetSessionHistoryQuery(
            GameId: gameId,
            StartDate: startDate,
            EndDate: endDate,
            Limit: limit,
            Offset: offset
        );

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSessionStats(
        [FromQuery] Guid? gameId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int topPlayersLimit,
        IMediator mediator,
                CancellationToken ct)
    {
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
    }


#pragma warning disable MA0051 // Method is too long - Complex image upload endpoint with security validation
    private static async Task<IResult> HandleUploadGameImage(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Auth check - Admin/Editor only
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        // SECURITY FIX: Validate Content-Type is multipart/form-data
        // Code review finding: Explicit validation improves error messages
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = "validation_failed",
                message = "Request must be multipart/form-data"
            });
        }

        // Read multipart form data
        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = "validation_failed",
                details = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["file"] = "No file provided"
                }
            });
        }

        // Parse form fields
        var gameId = form["gameId"].ToString();
        var imageTypeStr = form["imageType"].ToString();

        if (string.IsNullOrWhiteSpace(gameId))
        {
            return Results.BadRequest(new
            {
                error = "validation_failed",
                details = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gameId"] = "Game ID is required"
                }
            });
        }

        if (!Enum.TryParse<ImageType>(imageTypeStr, ignoreCase: true, out var imageType))
        {
            return Results.BadRequest(new
            {
                error = "validation_failed",
                details = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["imageType"] = "Invalid image type. Must be 'Icon' or 'Image'"
                }
            });
        }

        // Create command with file stream
        // SECURITY FIX: Copy to MemoryStream to avoid disposal issues with MediatR
        // Code review finding: Using statement may dispose before handler completes
        var memoryStream = new MemoryStream();
        try
        {
            var fileStream = file.OpenReadStream();
            await using (fileStream.ConfigureAwait(false))
            {
                await fileStream.CopyToAsync(memoryStream, ct).ConfigureAwait(false);
                memoryStream.Position = 0; // Reset for handler to read

                var command = new UploadGameImageCommand(
                    FileStream: memoryStream,
                    FileName: file.FileName,
                    GameId: gameId,
                    ImageType: imageType
                );

                var result = await mediator.Send(command, ct).ConfigureAwait(false);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "Image upload failed for game {GameId}, type {ImageType}: {Error}",
                        gameId,
                        imageType,
                        result.ErrorMessage
                    );

                    return Results.BadRequest(new
                    {
                        error = "upload_failed",
                        message = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully uploaded {ImageType} for game {GameId}: {FileId} ({Size} bytes)",
                    imageType,
                    gameId,
                    result.FileId,
                    result.FileSizeBytes
                );

                return Results.Ok(new
                {
                    success = true,
                    fileId = result.FileId,
                    fileUrl = result.FileUrl,
                    fileSizeBytes = result.FileSizeBytes
                });
            }
        }
        finally
        {
            // RESOURCE FIX: Dispose MemoryStream after use
            // Code review finding: Proper disposal of IDisposable resources
            await memoryStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void MapSessionStateEndpoints(RouteGroupBuilder group)
    {
        // ========================================
        // GameSessionState Endpoints - Issue #2403
        // ========================================

        // Initialize game state
        group.MapPost("/sessions/{sessionId}/state/initialize", HandleInitializeGameState)
        .RequireSession()
        .Produces<GameSessionStateDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State")
        .WithSummary("Initialize game state")
        .WithDescription("Creates initial game state for a session from a template. Requires active user session.");

        // Get current game state
        group.MapGet("/sessions/{sessionId}/state", HandleGetGameState)
        .RequireAuthenticatedUser()
        .Produces<GameSessionStateDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State")
        .WithSummary("Get current game state")
        .WithDescription("Returns the current state of a game session. Requires authentication.");

        // Update game state
        group.MapPatch("/sessions/{sessionId}/state", HandleUpdateGameState)
        .RequireSession()
        .Produces<GameSessionStateDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State")
        .WithSummary("Update game state")
        .WithDescription("Updates the current game state with new state data. Requires active user session.");

        // Create state snapshot
        group.MapPost("/sessions/{sessionId}/state/snapshots", HandleCreateStateSnapshot)
        .RequireSession()
        .Produces<GameStateSnapshotDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State", "Snapshots")
        .WithSummary("Create state snapshot")
        .WithDescription("Creates a snapshot of the current game state for save/restore functionality. Requires active user session.");

        // Get state snapshots
        group.MapGet("/sessions/{sessionId}/state/snapshots", HandleGetStateSnapshots)
        .RequireAuthenticatedUser()
        .Produces<List<GameStateSnapshotDto>>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State", "Snapshots")
        .WithSummary("Get state snapshots")
        .WithDescription("Returns all saved state snapshots for a game session. Requires authentication.");

        // Restore from snapshot
        group.MapPost("/sessions/{sessionId}/state/restore/{snapshotId}", HandleRestoreStateSnapshot)
        .RequireSession()
        .Produces<GameSessionStateDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "State", "Snapshots")
        .WithSummary("Restore from snapshot")
        .WithDescription("Restores game state from a previously saved snapshot. Requires active user session.");

        // ========================================
        // Player Mode Move Suggestions - Issue #2404
        // ========================================

        // Suggest moves for current game state
        group.MapPost("/sessions/{sessionId}/suggest-move", HandleSuggestMove)
        .RequireSession()
        .Produces<MoveSuggestionsDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "AI", "PlayerMode")
        .WithSummary("Suggest moves using AI")
        .WithDescription("Generates AI-powered move suggestions for the current game state using Player Mode agent. Requires active user session.");

        // Apply a move suggestion to game state
        group.MapPost("/sessions/{sessionId}/apply-suggestion", HandleApplySuggestion)
        .RequireSession()
        .Produces<GameSessionStateDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Sessions", "AI", "PlayerMode")
        .WithSummary("Apply AI move suggestion")
        .WithDescription("Applies a suggested move to the game state, updating the current position. Requires active user session.");
    }

    // Issue #2403: GameSessionState handlers
    private static async Task<IResult> HandleInitializeGameState(
        Guid sessionId,
        [FromBody] InitializeGameStateRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new InitializeGameStateCommand(
            GameSessionId: sessionId,
            TemplateId: request.TemplateId,
            InitialState: request.InitialState
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameState(
        Guid sessionId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateQuery(sessionId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleUpdateGameState(
        Guid sessionId,
        [FromBody] UpdateGameStateRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateQuery(sessionId);
        var state = await mediator.Send(query, ct).ConfigureAwait(false);

        if (state == null)
            return Results.NotFound();

        // Parse JSON with error handling
        JsonDocument parsedState;
        try
        {
            parsedState = JsonDocument.Parse(request.NewState);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = "Invalid JSON in NewState", details = ex.Message });
        }

        var command = new UpdateGameStateCommand(
            SessionStateId: state.Id,
            NewState: parsedState
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateStateSnapshot(
        Guid sessionId,
        [FromBody] CreateStateSnapshotRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateQuery(sessionId);
        var state = await mediator.Send(query, ct).ConfigureAwait(false);

        if (state == null)
            return Results.NotFound();

        var command = new CreateStateSnapshotCommand(
            SessionStateId: state.Id,
            TurnNumber: request.TurnNumber,
            Description: request.Description
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/sessions/{sessionId}/state/snapshots/{result.Id}", result);
    }

    private static async Task<IResult> HandleGetStateSnapshots(
        Guid sessionId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateQuery(sessionId);
        var state = await mediator.Send(query, ct).ConfigureAwait(false);

        if (state == null)
            return Results.NotFound();

        var snapshotsQuery = new GetStateSnapshotsQuery(state.Id);
        var result = await mediator.Send(snapshotsQuery, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRestoreStateSnapshot(
        Guid sessionId,
        Guid snapshotId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateQuery(sessionId);
        var state = await mediator.Send(query, ct).ConfigureAwait(false);

        if (state == null)
            return Results.NotFound();

        var command = new RestoreStateSnapshotCommand(
            SessionStateId: state.Id,
            SnapshotId: snapshotId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #2404: Player Mode Move Suggestion handlers
    private static async Task<IResult> HandleSuggestMove(
        Guid sessionId,
        [FromBody] SuggestMoveRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var command = new SuggestMoveCommand(
            SessionId: sessionId,
            AgentId: request.AgentId,
            Query: request.Query,
            UserId: session.User!.Id
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleApplySuggestion(
        Guid sessionId,
        [FromBody] ApplySuggestionRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var command = new ApplySuggestionCommand(
            SessionId: sessionId,
            SuggestionId: request.SuggestionId,
            StateChanges: request.StateChanges,
            UserId: session.User!.Id
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #3353: Similar Games Discovery handler
    private static async Task<IResult> HandleGetSimilarGames(
        Guid id,
        HttpContext context,
        IMediator mediator,
        [FromQuery] int? limit,
        [FromQuery] double? minSimilarity,
        CancellationToken ct)
    {
        // Try to get user ID if authenticated (for filtering owned games)
        Guid? userId = null;
        if (context.Items.TryGetValue(nameof(SessionStatusDto), out var sessionObj) &&
            sessionObj is SessionStatusDto session &&
            session.User != null)
        {
            userId = session.User.Id;
        }

        var query = new GetSimilarGamesQuery(
            GameId: id,
            UserId: userId,
            TopK: limit ?? 10,
            MinSimilarity: minSimilarity ?? 0.3
        );

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
