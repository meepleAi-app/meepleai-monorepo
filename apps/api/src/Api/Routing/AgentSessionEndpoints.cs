using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Extensions;
using Api.Infrastructure.Serialization;
using Api.Middleware;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Agent Session endpoints for session-based agent lifecycle.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4282: ogni rotta passa chi chiama e la sessione di gioco del percorso. Gli handler
/// rifiutano chi non e` il proprietario (403) e una sessione agente di un'altra sessione di gioco (404).
/// </summary>
internal static class AgentSessionEndpoints
{
    public static RouteGroupBuilder MapAgentSessionEndpoints(this RouteGroupBuilder group)
    {
        MapLaunchEndpoint(group);
        MapUpdateStateEndpoint(group);
        MapUpdateDefinitionEndpoint(group); // Issue #3252
        MapUpdateConfigEndpoint(group); // Issue #3253
        MapEndSessionEndpoint(group);

        return group;
    }

    private static void MapLaunchEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{gameSessionId:guid}/agent/launch", HandleLaunch)
            .WithName("LaunchSessionAgent")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Launch a new agent session for a game session");
    }

    private static void MapUpdateStateEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{gameSessionId:guid}/agent/state", HandleUpdateState)
            .WithName("UpdateAgentSessionState")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Update current game state for agent session");
    }

    private static void MapUpdateDefinitionEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/game-sessions/{gameSessionId:guid}/agent/definition", HandleUpdateDefinition)
            .WithName("UpdateAgentSessionDefinition")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Update agent definition during active session");
    }

    private static void MapUpdateConfigEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/game-sessions/{gameSessionId:guid}/agent/config", HandleUpdateConfig)
            .WithName("UpdateAgentSessionConfig")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Update agent runtime configuration");
    }

    private static void MapEndSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/game-sessions/{gameSessionId:guid}/agent", HandleEndSession)
            .WithName("EndSessionAgent")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("End agent session (preserves ChatLog history)");
    }

    private static async Task<IResult> HandleLaunch(
        [FromRoute] Guid gameSessionId,
        [FromBody] LaunchSessionAgentRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Session validated by RequireSessionFilter. Il ruolo serve al cancello RAG (#4282, ADR-096).
        var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;

        var command = new LaunchSessionAgentCommand(
            GameSessionId: gameSessionId,
            AgentDefinitionId: request.AgentDefinitionId,
            UserId: session.Principal!.Subject.Id,
            UserRole: session.Principal!.EffectiveActor.Role,
            GameId: request.GameId,
            InitialGameStateJson: request.InitialGameStateJson
        );

        var agentSessionId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new LaunchSessionAgentResponse(agentSessionId));
    }

    private static async Task<IResult> HandleUpdateState(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionStateRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionStateCommand(
            AgentSessionId: request.AgentSessionId,
            GameStateJson: request.GameStateJson,
            GameSessionId: gameSessionId,
            UserId: httpContext.User.GetUserId()
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateDefinition(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionDefinitionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionDefinitionCommand(
            AgentSessionId: request.AgentSessionId,
            NewAgentDefinitionId: request.NewAgentDefinitionId,
            GameSessionId: gameSessionId,
            UserId: httpContext.User.GetUserId()
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateConfig(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionConfigRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionConfigCommand(
            AgentSessionId: request.AgentSessionId,
            ModelType: request.ModelType,
            Temperature: request.Temperature,
            MaxTokens: request.MaxTokens,
            RagStrategy: request.RagStrategy,
            RagParams: request.RagParams,
            GameSessionId: gameSessionId,
            UserId: httpContext.User.GetUserId()
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleEndSession(
        [FromRoute] Guid gameSessionId,
        [FromBody] EndSessionAgentRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new EndSessionAgentCommand(
            AgentSessionId: request.AgentSessionId,
            GameSessionId: gameSessionId,
            UserId: httpContext.User.GetUserId()
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }
}

// Request/Response DTOs
// Issue #4154: AgentDefinitionId opzionale; assente, si usa l'agente di sistema (ADR-094).
internal record LaunchSessionAgentRequest(
    Guid? AgentDefinitionId,
    Guid GameId,
    string InitialGameStateJson);

internal record LaunchSessionAgentResponse(Guid AgentSessionId);

internal record UpdateAgentSessionStateRequest(
    Guid AgentSessionId,
    string GameStateJson);

internal record UpdateAgentSessionDefinitionRequest(
    Guid AgentSessionId,
    Guid NewAgentDefinitionId);

internal record UpdateAgentSessionConfigRequest(
    Guid AgentSessionId,
    string ModelType,
    double Temperature,
    int MaxTokens,
    string RagStrategy,
    IDictionary<string, object> RagParams);

internal record EndSessionAgentRequest(Guid AgentSessionId);
