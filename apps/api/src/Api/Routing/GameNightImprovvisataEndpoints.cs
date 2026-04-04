using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Api.Routing;

/// <summary>
/// Endpoints for the Game Night Improvvisata feature.
/// Handles BGG search, BGG import and session-level AI orchestration flows.
/// Game Night Improvvisata - E1-1: User-facing BGG search via CQRS.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
internal static class GameNightImprovvisataEndpoints
{
    public static RouteGroupBuilder MapGameNightImprovvisataEndpoints(this RouteGroupBuilder group)
    {
        var improvvisata = group.MapGroup("/game-night")
            .WithTags("GameNightImprovvisata");

        // E1-1: BGG search — admin only (BGG commercial use licensing restriction)
        improvvisata.MapGet("/bgg/search", HandleSearchBggGames)
            .RequireAdminSession()
            .RequireRateLimiting("BggSearch")
            .Produces<SearchBggGamesForGameNightResult>(200)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .WithName("GameNightSearchBggGames")
            .WithSummary("Search BoardGameGeek for games (Admin only)")
            .WithDescription("Admin-only search for board games on BoardGameGeek. Restricted due to BGG commercial use licensing. Rate limited to 20 searches per hour per user.");

        // E1-2: Import BGG game — admin only (BGG commercial use licensing restriction)
        improvvisata.MapPost("/import-bgg", HandleImportBggGame)
            .RequireAdminSession()
            .Produces<ImportBggGameResult>(201)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(409)
            .Produces(429)
            .WithName("ImportBggGame")
            .WithSummary("Import a BGG game as a PrivateGame (Admin only)")
            .WithDescription("Admin-only import of a game from BoardGameGeek into the user's private library. Restricted due to BGG commercial use licensing.");

        // E2-1: Start improvvisata session from a PrivateGame
        improvvisata.MapPost("/start-session", HandleStartImprovvisataSession)
            .RequireAuthorization()
            .Produces<StartImprovvisataSessionResponse>(200)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithName("StartImprovvisataSession")
            .WithSummary("Start a game night session from a PrivateGame")
            .WithDescription("Creates a LiveGameSession + SessionInvite in one transaction. Host is added automatically. Returns the session ID, invite code (PIN), and shareable join link.");

        // E3: Arbitro mode — submit a rule dispute during an in-progress session
        improvvisata.MapPost("/sessions/{sessionId:guid}/disputes", HandleSubmitRuleDispute)
            .RequireAuthorization()
            .Produces<RuleDisputeResponse>(200)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .Produces(409)
            .WithName("SubmitRuleDispute")
            .WithSummary("Submit a rule dispute for AI arbitration")
            .WithDescription("Sends a rule question to the AI arbitro, which returns a structured verdict with rule references. The dispute is stored on the live session for later review.");

        // E4: Save — pause session and create a full-state PauseSnapshot
        improvvisata.MapPost("/sessions/{sessionId:guid}/save", HandleSaveSession)
            .RequireAuthorization()
            .Produces<SaveSessionResponse>(200)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .Produces(409)
            .WithName("SaveSession")
            .WithSummary("Pause and save a live game session")
            .WithDescription("Creates a full-state PauseSnapshot and transitions the session to Paused. An async AI summary is generated in the background. Returns the snapshot ID.");

        // E4: Resume — restore a paused session from its latest PauseSnapshot
        improvvisata.MapPost("/sessions/{sessionId:guid}/resume", HandleResumeSession)
            .RequireAuthorization()
            .Produces<ResumeSessionResponse>(200)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .Produces(409)
            .WithName("ResumeSession")
            .WithSummary("Resume a paused game session")
            .WithDescription("Resumes a paused session from its latest PauseSnapshot. Issues a fresh invite code and returns an optional agent recap for context.");

        return group;
    }

    #region Query Handlers

    private static async Task<IResult> HandleSearchBggGames(
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "page")] int page,
        [FromQuery(Name = "pageSize")] int pageSize,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new SearchBggGamesForGameNightQuery(q ?? string.Empty, page, pageSize);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    #endregion

    #region Command Handlers

    private static async Task<IResult> HandleStartImprovvisataSession(
        [FromBody] StartImprovvisataSessionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new StartImprovvisataSessionCommand(
            UserId: userId,
            PrivateGameId: request.PrivateGameId);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleImportBggGame(
        [FromBody] ImportBggGameRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new ImportBggGameCommand(
            UserId: userId,
            BggId: request.BggId);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-night/private-games/{result.PrivateGameId}", result);
    }

    private static async Task<IResult> HandleSubmitRuleDispute(
        Guid sessionId,
        [FromBody] SubmitRuleDisputeRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new SubmitRuleDisputeCommand(
            SessionId: sessionId,
            CallerUserId: userId,
            Description: request.Description,
            RaisedByPlayerName: request.RaisedByPlayerName);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSaveSession(
        Guid sessionId,
        [FromBody] SaveSessionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CreatePauseSnapshotCommand(
            SessionId: sessionId,
            SavedByUserId: userId,
            FinalPhotoIds: request.FinalPhotoIds);

        var snapshotId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new SaveSessionResponse(snapshotId));
    }

    private static async Task<IResult> HandleResumeSession(
        Guid sessionId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new ResumeSessionFromSnapshotCommand(
            SessionId: sessionId,
            ResumedByUserId: userId);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    #endregion

    #region Request Records

    private sealed record ImportBggGameRequest(int BggId);

    private sealed record StartImprovvisataSessionRequest(Guid PrivateGameId);

    private sealed record SubmitRuleDisputeRequest(string Description, string RaisedByPlayerName);

    private sealed record SaveSessionRequest(List<Guid>? FinalPhotoIds = null);

    private sealed record SaveSessionResponse(Guid SnapshotId);

    #endregion
}
