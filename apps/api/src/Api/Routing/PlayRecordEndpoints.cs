using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Play record endpoints for tracking game play history.
/// Issue #3889-#3890: CQRS commands and queries for play records.
/// </summary>
internal static class PlayRecordEndpoints
{
    public static RouteGroupBuilder MapPlayRecordEndpoints(this RouteGroupBuilder group)
    {
        // Commands
        group.MapPost("/play-records", HandleCreatePlayRecord)
            .RequireAuthenticatedUser()
            .Produces<Guid>(201)
            .Produces(400)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Create a play record")
            .WithDescription("Creates a new play record for a game session. Supports catalog games or free-form games.");

        group.MapPost("/play-records/{recordId}/players", HandleAddPlayer)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Add player to play record")
            .WithDescription("Adds a registered user or guest player to a play record.");

        group.MapPost("/play-records/{recordId}/scores", HandleRecordScore)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Record a score")
            .WithDescription("Records a score for a player in a play record. Supports multi-dimensional scoring.");

        group.MapPost("/play-records/{recordId}/start", HandleStartRecord)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Start play record")
            .WithDescription("Marks a play record as in-progress.");

        group.MapPost("/play-records/{recordId}/complete", HandleCompleteRecord)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Complete play record")
            .WithDescription("Completes a play record with optional manual duration.");

        group.MapPut("/play-records/{recordId}", HandleUpdateRecord)
            .RequireAuthenticatedUser()
            .Produces(204)
            .Produces(404)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Update play record details")
            .WithDescription("Updates play record details. Allowed even after completion.");

        // Queries
        group.MapGet("/play-records/{recordId}", HandleGetPlayRecord)
            .RequireAuthenticatedUser()
            .Produces<PlayRecordDto>(200)
            .Produces(404)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Get play record by ID")
            .WithDescription("Retrieves full details of a play record including players and scores.");

        group.MapGet("/play-records/history", HandleGetUserHistory)
            .RequireAuthenticatedUser()
            .Produces<PlayHistoryResponse>(200)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Get user play history")
            .WithDescription("Retrieves paginated play history for the authenticated user. Supports game filtering.");

        group.MapGet("/play-records/statistics", HandleGetStatistics)
            .RequireAuthenticatedUser()
            .Produces<PlayerStatisticsDto>(200)
            .Produces(401)
            .WithTags("PlayRecords")
            .WithSummary("Get player statistics")
            .WithDescription("Retrieves cross-game statistics for the authenticated user.");

        return group;
    }

    #region Command Handlers

    private static async Task<IResult> HandleCreatePlayRecord(
        [FromBody] CreatePlayRecordRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new CreatePlayRecordCommand(
            userId,
            request.GameId,
            request.GameName,
            request.SessionDate,
            request.Visibility,
            request.GroupId,
            request.ScoringDimensions,
            request.DimensionUnits);

        var recordId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-management/play-records/{recordId}", recordId);
    }

    private static async Task<IResult> HandleAddPlayer(
        Guid recordId,
        [FromBody] AddPlayerRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddPlayerToRecordCommand(recordId, request.UserId, request.DisplayName);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleRecordScore(
        Guid recordId,
        [FromBody] RecordScoreRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RecordScoreCommand(recordId, request.PlayerId, request.Dimension, request.Value, request.Unit);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleStartRecord(
        Guid recordId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new StartPlayRecordCommand(recordId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleCompleteRecord(
        Guid recordId,
        [FromBody] CompleteRecordRequest? request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CompletePlayRecordCommand(recordId, request?.ManualDuration);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateRecord(
        Guid recordId,
        [FromBody] UpdateRecordRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePlayRecordCommand(recordId, request.SessionDate, request.Notes, request.Location);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    #endregion

    #region Query Handlers

    private static async Task<IResult> HandleGetPlayRecord(
        Guid recordId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetPlayRecordQuery(recordId);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserHistory(
        [AsParameters] GetHistoryQueryParams queryParams,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var query = new GetUserPlayHistoryQuery(userId, queryParams.Page, queryParams.PageSize, queryParams.GameId);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetStatistics(
        [AsParameters] GetStatisticsQueryParams queryParams,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var query = new GetPlayerStatisticsQuery(userId, queryParams.StartDate, queryParams.EndDate);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    #endregion

    #region Request/Response Models

    private record CreatePlayRecordRequest(
        Guid? GameId,
        string GameName,
        DateTime SessionDate,
        PlayRecordVisibility Visibility,
        Guid? GroupId = null,
        List<string>? ScoringDimensions = null,
        Dictionary<string, string>? DimensionUnits = null);

    private record AddPlayerRequest(Guid? UserId, string DisplayName);

    private record RecordScoreRequest(Guid PlayerId, string Dimension, int Value, string? Unit);

    private record CompleteRecordRequest(TimeSpan? ManualDuration);

    private record UpdateRecordRequest(DateTime? SessionDate, string? Notes, string? Location);

    private record GetHistoryQueryParams(int Page = 1, int PageSize = 20, Guid? GameId = null);

    private record GetStatisticsQueryParams(DateTime? StartDate = null, DateTime? EndDate = null);

    #endregion
}
