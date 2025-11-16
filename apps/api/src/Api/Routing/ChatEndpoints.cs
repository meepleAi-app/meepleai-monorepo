using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Routing;

/// <summary>
/// Chat management endpoints.
/// Handles chat CRUD, messages, and export operations.
/// </summary>
public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/chats", async (HttpContext context, ChatService chatService, string? gameId, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var chats = string.IsNullOrWhiteSpace(gameId)
                ? await chatService.GetUserChatsAsync(session.User.Id, 50, ct)
                : await chatService.GetUserChatsByGameAsync(session.User.Id, gameId, 50, ct);

            var response = chats.Select(c => new ChatDto(
                c.Id,
                c.GameId.ToString(),
                c.Game.Name,
                c.AgentId.ToString(),
                c.Agent.Name,
                c.StartedAt,
                c.LastMessageAt
            )).ToList();

            return Results.Json(response);
        });

        group.MapGet("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            var chat = await chatService.GetChatByIdAsync(chatId, session.User.Id, ct);
            if (chat == null)
            {
                throw new NotFoundException("Chat", chatId.ToString());
            }

            var messages = chat.Logs.Select(l => new ChatMessageDto(
                l.Id,
                l.Level,
                l.Message,
                l.MetadataJson,
                l.CreatedAt
            )).ToList();

            var response = new ChatWithHistoryDto(
                chat.Id,
                chat.GameId.ToString(),
                chat.Game.Name,
                chat.AgentId.ToString(),
                chat.Agent.Name,
                chat.StartedAt,
                chat.LastMessageAt,
                messages
            );

            return Results.Json(response);
        });

        group.MapPost("/chats", async (CreateChatRequest? request, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (request == null)
            {
                throw new BadRequestException("Request body is required");
            }

            if (string.IsNullOrWhiteSpace(request.GameId) || string.IsNullOrWhiteSpace(request.AgentId))
            {
                throw new BadRequestException("GameId and AgentId are required");
            }

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            var chat = await chatService.CreateChatAsync(session.User.Id, request.GameId, request.AgentId, ct);

            // Reload with navigations
            var fullChat = await chatService.GetChatByIdAsync(chat.Id, session.User.Id, ct);
            if (fullChat == null)
            {
                throw new InvalidOperationException("Failed to retrieve created chat");
            }

            var response = new ChatDto(
                fullChat.Id,
                fullChat.GameId.ToString(),
                fullChat.Game.Name,
                fullChat.AgentId.ToString(),
                fullChat.Agent.Name,
                fullChat.StartedAt,
                fullChat.LastMessageAt
            );

            logger.LogInformation("User {UserId} created chat {ChatId} for game {GameId}", session.User.Id, chat.Id, request.GameId);
            return Results.Created($"/chats/{chat.Id}", response);
        });

        group.MapDelete("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            var deleted = await chatService.DeleteChatAsync(chatId, session.User.Id, ct);
            if (!deleted)
            {
                throw new NotFoundException("Chat", chatId.ToString());
            }

            logger.LogInformation("User {UserId} deleted chat {ChatId}", session.User.Id, chatId);
            return Results.NoContent();
        });

        // CHAT-06: Message editing endpoint
        group.MapPut("/chats/{chatId:guid}/messages/{messageId:guid}",
            async (
                Guid chatId,
                Guid messageId,
                UpdateMessageRequest request,
                HttpContext context,
                ChatService chatService,
                IFeatureFlagService featureFlags,
                ILogger<Program> logger,
                CancellationToken ct) =>
            {
                if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
                {
                    return Results.Unauthorized();
                }

                // CONFIG-05: Check if message edit/delete feature is enabled
                if (!await featureFlags.IsEnabledAsync("Features.MessageEditDelete"))
                {
                    return Results.Json(
                        new { error = "feature_disabled", message = "Message modification is currently disabled", featureName = "Features.MessageEditDelete" },
                        statusCode: 403);
                }

                // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
                logger.LogInformation("User {UserId} updating message {MessageId} in chat {ChatId}", session.User.Id, messageId, chatId);
                var updatedMessage = await chatService.UpdateMessageAsync(chatId, messageId, request.Content, session.User.Id, ct);

                var response = MapToChatMessageResponse(updatedMessage);
                logger.LogInformation("Message {MessageId} updated successfully by user {UserId}", messageId, session.User.Id);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("UpdateChatMessage")
            .WithDescription("Edit the content of an existing user message. Invalidates subsequent AI responses. Requires authentication. Users can only edit their own messages.")
            .WithTags("Chat");

        // CHAT-06: Message deletion endpoint
        group.MapDelete("/chats/{chatId:guid}/messages/{messageId:guid}",
            async (
                Guid chatId,
                Guid messageId,
                HttpContext context,
                ChatService chatService,
                IFeatureFlagService featureFlags,
                ILogger<Program> logger,
                CancellationToken ct) =>
            {
                if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
                {
                    return Results.Unauthorized();
                }

                // CONFIG-05: Check if message edit/delete feature is enabled
                if (!await featureFlags.IsEnabledAsync("Features.MessageEditDelete"))
                {
                    return Results.Json(
                        new { error = "feature_disabled", message = "Message modification is currently disabled", featureName = "Features.MessageEditDelete" },
                        statusCode: 403);
                }

                var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

                // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
                logger.LogInformation("User {UserId} deleting message {MessageId} in chat {ChatId} (admin: {IsAdmin})", session.User.Id, messageId, chatId, isAdmin);
                var deleted = await chatService.DeleteMessageAsync(chatId, messageId, session.User.Id, isAdmin, ct);

                if (!deleted)
                {
                    logger.LogInformation("Message {MessageId} already deleted", messageId);
                    return Results.Ok(new { message = "Message already deleted" });
                }

                logger.LogInformation("Message {MessageId} deleted successfully by user {UserId}", messageId, session.User.Id);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("DeleteChatMessage")
            .WithDescription("Soft-delete a message. Users can delete their own messages; admins can delete any message. Invalidates subsequent AI responses. Requires authentication.")
            .WithTags("Chat");

        // LEGACY CHAT-05: Export chat endpoint - REMOVED
        // Replaced by DDD implementation in KnowledgeBaseEndpoints.cs
        // New endpoint: GET /api/v1/chat-threads/{threadId}/export?format=json|markdown

        return group;
    }

    // CHAT-06: Helper method to map ChatLogEntity to ChatMessageResponse
    private static ChatMessageResponse MapToChatMessageResponse(ChatLogEntity entity)
    {
        return new ChatMessageResponse(
            entity.Id,
            entity.ChatId,
            entity.UserId?.ToString(),
            entity.Level,
            entity.Message,
            entity.SequenceNumber,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId?.ToString(),
            entity.IsInvalidated,
            entity.MetadataJson
        );
    }
}
