using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
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
        MapFaqEndpoints(group);

        return group;
    }

    private static void MapGameRetrievalEndpoints(RouteGroupBuilder group)
    {
        // Get all games (DDD/CQRS) with pagination support
        group.MapGet("/games", HandleGetAllGames)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get game by ID (DDD/CQRS)
        group.MapGet("/games/{id}", HandleGetGameById)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get all sessions for a game (DDD/CQRS)
        // Issue #1675: Frontend needs game sessions listing
        group.MapGet("/games/{id}/sessions", HandleGetGameSessions)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get game details with extended metadata and statistics (DDD/CQRS)
        // Issue #1196: Supports Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/details", HandleGetGameDetails)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get rule specifications for a game (DDD/CQRS)
        // Issue #1196: Supports Rules tab in Game Detail Page (Issue #855)
        group.MapGet("/games/{id}/rules", HandleGetGameRules)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)
    }

    private static void MapGameManagementEndpoints(RouteGroupBuilder group)
    {
        // Create game (DDD/CQRS) - Admin/Editor only
        group.MapPost("/games", HandleCreateGame);

        // Update game (DDD/CQRS) - Admin/Editor only
        group.MapPut("/games/{id}", HandleUpdateGame);

        // Upload game image (icon or cover) - Issue #2255
        group.MapPost("/games/upload-image", HandleUploadGameImage)
        .DisableAntiforgery(); // Required for multipart/form-data file uploads
    }

    private static void MapSessionLifecycleEndpoints(RouteGroupBuilder group)
    {
        // ========================================
        // GameSession CQRS Endpoints
        // ========================================

        // Start game session
        group.MapPost("/sessions", HandleStartSession)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Add player to session
        group.MapPost("/sessions/{id}/players", HandleAddPlayer)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Complete game session
        group.MapPost("/sessions/{id}/complete", HandleCompleteSession)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Abandon game session
        group.MapPost("/sessions/{id}/abandon", HandleAbandonSession)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Pause game session
        group.MapPost("/sessions/{id}/pause", HandlePauseSession)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Resume game session
        group.MapPost("/sessions/{id}/resume", HandleResumeSession)
        .RequireSession(); // Issue #1446: Automatic session validation

        // End game session (alias for complete)
        group.MapPost("/sessions/{id}/end", HandleEndSession)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapSessionQueryEndpoints(RouteGroupBuilder group)
    {
        // Get session by ID
        group.MapGet("/sessions/{id}", HandleGetSessionById)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get active sessions for game
        group.MapGet("/games/{gameId}/sessions/active", HandleGetActiveSessionsByGame)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get all active sessions (with pagination)
        group.MapGet("/sessions/active", HandleGetActiveSessions)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get session history (with filters and pagination)
        group.MapGet("/sessions/history", HandleGetSessionHistory)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Get session statistics (aggregated stats with filters)
        group.MapGet("/sessions/statistics", HandleGetSessionStats)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)
    }

    private static void MapFaqEndpoints(RouteGroupBuilder group)
    {
        // ========================================
        // GameFAQ CQRS Endpoints (Issue #2028)
        // ========================================

        // Get FAQs for a game (public, paginated)
        group.MapGet("/games/{gameId}/faqs", HandleGetGameFaqs)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)

        // Create FAQ for a game (admin/editor only)
        group.MapPost("/games/{gameId}/faqs", HandleCreateGameFaq);

        // Update FAQ (admin/editor only)
        group.MapPut("/faqs/{id}", HandleUpdateGameFaq);

        // Delete FAQ (admin/editor only)
        group.MapDelete("/faqs/{id}", HandleDeleteGameFaq);

        // Upvote FAQ (public)
        group.MapPost("/faqs/{id}/upvote", HandleUpvoteGameFaq)
        .RequireAuthenticatedUser(); // Issue #1446: Dual authentication (session OR API key)
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
            BggId: request.BggId
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

    private static async Task<IResult> HandleStartSession(
        StartGameSessionRequest request,
        IMediator mediator,
                ILogger<Program> logger,
        CancellationToken ct)
    {
        var command = new StartGameSessionCommand(
            GameId: request.GameId,
            Players: request.Players
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Started game session {SessionId} for game {GameId}", result.Id, result.GameId);
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

    private static async Task<IResult> HandleGetGameFaqs(
        Guid gameId,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        IMediator mediator,
                CancellationToken ct)
    {
        var query = new GetGameFAQsQuery(
            GameId: gameId,
            Limit: limit ?? 10,
            Offset: offset ?? 0
        );

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateGameFaq(
        Guid gameId,
        CreateGameFAQRequest request,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Auth check
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new CreateGameFAQCommand(
            GameId: gameId,
            Question: request.Question,
            Answer: request.Answer
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Created FAQ {FAQId} for game {GameId}", result.Id, gameId);
        return Results.Created($"/api/v1/faqs/{result.Id}", result);
    }

    private static async Task<IResult> HandleUpdateGameFaq(
        Guid id,
        UpdateGameFAQRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Auth check
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new UpdateGameFAQCommand(
            Id: id,
            Question: request.Question,
            Answer: request.Answer
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteGameFaq(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Auth check
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new DeleteGameFAQCommand(Id: id);
        await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpvoteGameFaq(
        Guid id,
        IMediator mediator,
                CancellationToken ct)
    {
        var command = new UpvoteGameFAQCommand(Id: id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUploadGameImage(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Auth check - Admin/Editor only
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

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
        using var fileStream = file.OpenReadStream();
        var command = new UploadGameImageCommand(
            FileStream: fileStream,
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
