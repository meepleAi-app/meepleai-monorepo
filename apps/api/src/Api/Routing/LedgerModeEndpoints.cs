using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable MA0048 // File name must match type name
namespace Api.Routing;

/// <summary>
/// Ledger Mode endpoints for game state tracking.
/// Issue #2405 - Ledger Mode full state tracking
/// </summary>
internal static class LedgerModeEndpoints
{
    public static RouteGroupBuilder MapLedgerModeEndpoints(this RouteGroupBuilder group)
    {
        MapParseLedgerMessageEndpoint(group);
        MapConfirmStateChangeEndpoint(group);
        MapGetLedgerHistoryEndpoint(group);

        return group;
    }

    /// <summary>
    /// POST /api/v1/sessions/{sessionId}/ledger/parse
    /// Parse a chat message for game state changes
    /// </summary>
    private static void MapParseLedgerMessageEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/sessions/{sessionId:guid}/ledger/parse", async (
            Guid sessionId,
            ParseLedgerMessageRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new ParseLedgerMessageCommand(
                SessionId: sessionId,
                Message: req.Message,
                UserId: session!.User!.Id
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Parsed ledger message for session {SessionId}: ChangeType={ChangeType}, Confidence={Confidence}",
                sessionId,
                result.ChangeType,
                result.Confidence);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("ParseLedgerMessage")
        .WithTags("Ledger Mode")
        .Produces<LedgerParseResultDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(500);
    }

    /// <summary>
    /// POST /api/v1/sessions/{sessionId}/ledger/confirm
    /// Confirm and apply state changes to game session
    /// </summary>
    private static void MapConfirmStateChangeEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/sessions/{sessionId:guid}/ledger/confirm", async (
            Guid sessionId,
            ConfirmStateChangeRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new ConfirmStateChangeCommand(
                SessionId: sessionId,
                StateChanges: req.StateChanges,
                UserId: session!.User!.Id,
                Description: req.Description
            );

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Confirmed state change for session {SessionId} with {ChangeCount} changes",
                sessionId,
                req.StateChanges.Count);

            return Results.NoContent();
        })
        .RequireSession()
        .WithName("ConfirmStateChange")
        .WithTags("Ledger Mode")
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(500);
    }

    /// <summary>
    /// GET /api/v1/sessions/{sessionId}/ledger/history
    /// Retrieve ledger state change history
    /// </summary>
    private static void MapGetLedgerHistoryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/sessions/{sessionId:guid}/ledger/history", async (
            Guid sessionId,
            [FromQuery] int? limit,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetLedgerHistoryQuery(
                SessionId: sessionId,
                Limit: limit ?? 50
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Retrieved ledger history for session {SessionId}: {ChangeCount} changes",
                sessionId,
                result.Changes.Count);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetLedgerHistory")
        .WithTags("Ledger Mode")
        .Produces<LedgerHistoryDto>(200)
        .Produces(401)
        .Produces(404)
        .Produces(500);
    }
}

/// <summary>
/// Request DTO for parsing ledger messages
/// </summary>
internal sealed record ParseLedgerMessageRequest(string Message);

/// <summary>
/// Request DTO for confirming state changes
/// </summary>
internal sealed record ConfirmStateChangeRequest(
    Dictionary<string, object> StateChanges,
    string? Description = null);
