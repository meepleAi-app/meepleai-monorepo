using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Chat Session endpoints for chat history persistence.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal static class ChatSessionEndpoints
{
    public static RouteGroupBuilder MapChatSessionEndpoints(this RouteGroupBuilder group)
    {
        MapCreateSessionEndpoint(group);
        MapAddMessageEndpoint(group);
        MapGetSessionEndpoint(group);
        MapGetUserGameSessionsEndpoint(group);
        MapGetRecentSessionsEndpoint(group);
        MapGetSessionLimitEndpoint(group);
        MapDeleteSessionEndpoint(group);
        MapRenameSessionEndpoint(group);

        return group;
    }

    private static void MapCreateSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/chat/sessions", HandleCreateSession)
            .WithName("CreateChatSession")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Create a new chat session");
    }

    private static void MapAddMessageEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/chat/sessions/{sessionId:guid}/messages", HandleAddMessage)
            .WithName("AddChatSessionMessage")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Add a message to a chat session");
    }

    private static void MapGetSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/chat/sessions/{sessionId:guid}", HandleGetSession)
            .WithName("GetChatSession")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Get a chat session with messages");
    }

    private static void MapGetUserGameSessionsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/{userId:guid}/games/{gameId:guid}/chat-sessions", HandleGetUserGameSessions)
            .WithName("GetUserGameChatSessions")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Get chat sessions for a user and game");
    }

    private static void MapGetRecentSessionsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/{userId:guid}/chat-sessions/recent", HandleGetRecentSessions)
            .WithName("GetRecentChatSessions")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Get recent chat sessions for a user");
    }

    private static void MapGetSessionLimitEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/{userId:guid}/chat-sessions/limit", HandleGetSessionLimit)
            .WithName("GetChatSessionLimit")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Get the user's chat session tier limit, usage, and tier name");
    }

    private static void MapDeleteSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/chat/sessions/{sessionId:guid}", HandleDeleteSession)
            .WithName("DeleteChatSession")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Delete a chat session");
    }

    private static void MapRenameSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/chat/sessions/{sessionId:guid}/rename", HandleRenameSession)
            .WithName("RenameChatSession")
            .RequireSession()
            .WithTags("ChatSessions")
            .WithDescription("Rename a chat session. Returns 400 with disambiguation hint if the UUID belongs to a ChatThread instead of a ChatSession.");
    }

    private static async Task<IResult> HandleCreateSession(
        [FromBody] CreateChatSessionRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        // Determine tier limit for auto-archive sliding window
        var (authenticated, session, _) = httpContext.TryGetActiveSession();
        var tierLimit = 0;
        if (authenticated && session?.Principal?.Subject != null)
            tierLimit = ChatSessionTierLimits.GetLimit(session.Principal!.Subject.Tier, session.Principal!.Subject.Role);

        var command = new CreateChatSessionCommand(
            UserId: userId,
            GameId: request.GameId,
            Title: request.Title,
            UserLibraryEntryId: request.UserLibraryEntryId,
            AgentSessionId: request.AgentSessionId,
            AgentConfigJson: request.AgentConfigJson,
            AgentId: request.AgentId,
            AgentType: request.AgentType,
            AgentName: request.AgentName,
            TierLimit: tierLimit,
            UserRole: session?.Principal?.Subject?.Role
        );

        var sessionId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/chat/sessions/{sessionId}", new CreateChatSessionResponse(sessionId));
    }

    private static async Task<IResult> HandleAddMessage(
        [FromRoute] Guid sessionId,
        [FromBody] AddChatSessionMessageRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddChatSessionMessageCommand(
            SessionId: sessionId,
            Role: request.Role,
            Content: request.Content,
            Metadata: request.Metadata
        );

        var messageId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new AddChatSessionMessageResponse(messageId));
    }

    private static async Task<IResult> HandleGetSession(
        [FromRoute] Guid sessionId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromServices] IMediator mediator = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetChatSessionQuery(
            SessionId: sessionId,
            MessageSkip: skip,
            MessageTake: take
        );

        var session = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(session);
    }

    private static async Task<IResult> HandleGetUserGameSessions(
        [FromRoute] Guid userId,
        [FromRoute] Guid gameId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromServices] IMediator mediator = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserGameChatSessionsQuery(
            UserId: userId,
            GameId: gameId,
            Skip: skip,
            Take: take
        );

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetRecentSessions(
        [FromRoute] Guid userId,
        [FromQuery] int limit = 10,
        [FromServices] IMediator mediator = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRecentChatSessionsQuery(
            UserId: userId,
            Limit: limit
        );

        var sessions = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(sessions);
    }

    private static async Task<IResult> HandleGetSessionLimit(
        [FromRoute] Guid userId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Ownership check: caller can only query their own limit
        var callerId = httpContext.User.GetUserId();
        if (callerId != userId)
            return Results.Forbid();

        var (authenticated, session, _) = httpContext.TryGetActiveSession();

        string tierName;
        int limit;

        if (authenticated && session?.Principal?.Subject != null)
        {
            tierName = session.Principal!.Subject.Tier ?? "user";
            limit = ChatSessionTierLimits.GetLimit(session.Principal!.Subject.Tier, session.Principal!.Subject.Role);
        }
        else
        {
            tierName = "anonymous";
            limit = ChatSessionTierLimits.GetLimit(null, null);
        }

        var used = await mediator.Send(new GetChatSessionCountQuery(callerId), cancellationToken).ConfigureAwait(false);

        return Results.Ok(new ChatSessionTierLimitDto(
            Limit: limit,
            Used: used,
            Tier: tierName));
    }

    private static async Task<IResult> HandleDeleteSession(
        [FromRoute] Guid sessionId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteChatSessionCommand(SessionId: sessionId);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleRenameSession(
        [FromRoute] Guid sessionId,
        [FromBody] RenameChatSessionRequest body,
        [FromServices] IMediator mediator,
        [FromServices] IChatSessionRepository sessionRepository,
        [FromServices] IChatThreadRepository threadRepository,
        CancellationToken cancellationToken)
    {
        // Naming disambiguation: detect if caller passed a ChatThread UUID to a ChatSession endpoint
        var sessionExists = await sessionRepository.ExistsAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (!sessionExists)
        {
            var thread = await threadRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (thread != null)
            {
                return Results.BadRequest(new
                {
                    error = "INVALID_ENTITY_TYPE",
                    hint = "The provided UUID belongs to a ChatThread (use POST /chat/threads/{id}/title) not a ChatSession"
                });
            }

            return Results.NotFound();
        }

        var command = new RenameChatSessionCommand(SessionId: sessionId, Title: body.Title);

        try
        {
            var dto = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(dto);
        }
        catch (Api.Middleware.Exceptions.NotFoundException)
        {
            return Results.NotFound();
        }
    }
}

// Request/Response DTOs
internal record CreateChatSessionRequest(
    Guid GameId,
    string? Title = null,
    Guid? UserLibraryEntryId = null,
    Guid? AgentSessionId = null,
    string? AgentConfigJson = null,
    Guid? AgentId = null,
    string? AgentType = null,
    string? AgentName = null);

internal record CreateChatSessionResponse(Guid SessionId);

internal record AddChatSessionMessageRequest(
    string Role,
    string Content,
    Dictionary<string, object>? Metadata = null);

internal record AddChatSessionMessageResponse(Guid MessageId);

internal record RenameChatSessionRequest(string Title);
