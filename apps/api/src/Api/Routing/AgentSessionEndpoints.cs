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
/// </summary>
internal static class AgentSessionEndpoints
{
    public static RouteGroupBuilder MapAgentSessionEndpoints(this RouteGroupBuilder group)
    {
        MapLaunchEndpoint(group);
        MapChatEndpoint(group);
        MapUpdateStateEndpoint(group);
        MapUpdateTypologyEndpoint(group); // Issue #3252
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

    private static void MapChatEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{gameSessionId:guid}/agent/chat", HandleChat)
            .WithName("ChatWithSessionAgent")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Chat with session agent (SSE streaming)");
    }

    private static void MapUpdateStateEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{gameSessionId:guid}/agent/state", HandleUpdateState)
            .WithName("UpdateAgentSessionState")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Update current game state for agent session");
    }

    private static void MapUpdateTypologyEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/game-sessions/{gameSessionId:guid}/agent/typology", HandleUpdateTypology)
            .WithName("UpdateAgentSessionTypology")
            .RequireSession()
            .WithTags("AgentSessions")
            .WithDescription("Update agent typology during active session");
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
        var userId = httpContext.User.GetUserId();

        var command = new LaunchSessionAgentCommand(
            GameSessionId: gameSessionId,
            TypologyId: request.TypologyId,
            UserId: userId,
            AgentId: request.AgentId,
            GameId: request.GameId,
            InitialGameStateJson: request.InitialGameStateJson
        );

        var agentSessionId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new LaunchSessionAgentResponse(agentSessionId));
    }

    private static async Task HandleChat(
        [FromRoute] Guid gameSessionId,
        [FromBody] ChatWithSessionAgentRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new ChatWithSessionAgentCommand(
            AgentSessionId: request.AgentSessionId,
            UserQuestion: request.UserQuestion,
            UserId: userId,
            ChatThreadId: request.ChatThreadId
        );

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        await foreach (var @event in mediator.CreateStream(command, cancellationToken).ConfigureAwait(false))
        {
            await httpContext.Response.WriteAsync(
                $"data: {System.Text.Json.JsonSerializer.Serialize(@event, SseJsonOptions.Default)}\n\n",
                cancellationToken).ConfigureAwait(false);

            await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IResult> HandleUpdateState(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionStateRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionStateCommand(
            AgentSessionId: request.AgentSessionId,
            GameStateJson: request.GameStateJson
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateTypology(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionTypologyRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionTypologyCommand(
            AgentSessionId: request.AgentSessionId,
            NewTypologyId: request.NewTypologyId
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateConfig(
        [FromRoute] Guid gameSessionId,
        [FromBody] UpdateAgentSessionConfigRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAgentSessionConfigCommand(
            AgentSessionId: request.AgentSessionId,
            ModelType: request.ModelType,
            Temperature: request.Temperature,
            MaxTokens: request.MaxTokens,
            RagStrategy: request.RagStrategy,
            RagParams: request.RagParams
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleEndSession(
        [FromRoute] Guid gameSessionId,
        [FromBody] EndSessionAgentRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new EndSessionAgentCommand(
            AgentSessionId: request.AgentSessionId
        );

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }
}

// Request/Response DTOs
internal record LaunchSessionAgentRequest(
    Guid TypologyId,
    Guid AgentId,
    Guid GameId,
    string InitialGameStateJson);

internal record LaunchSessionAgentResponse(Guid AgentSessionId);

internal record ChatWithSessionAgentRequest(
    Guid AgentSessionId,
    string UserQuestion,
    Guid? ChatThreadId = null);

internal record UpdateAgentSessionStateRequest(
    Guid AgentSessionId,
    string GameStateJson);

internal record UpdateAgentSessionTypologyRequest(
    Guid AgentSessionId,
    Guid NewTypologyId);

internal record UpdateAgentSessionConfigRequest(
    Guid AgentSessionId,
    string ModelType,
    double Temperature,
    int MaxTokens,
    string RagStrategy,
    IDictionary<string, object> RagParams);

internal record EndSessionAgentRequest(Guid AgentSessionId);
