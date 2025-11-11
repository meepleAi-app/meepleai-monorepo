using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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

            var chat = await chatService.GetChatByIdAsync(chatId, session.User.Id, ct);
            if (chat == null)
            {
                return Results.NotFound(new { error = "Chat not found" });
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
                return Results.BadRequest(new { error = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.GameId) || string.IsNullOrWhiteSpace(request.AgentId))
            {
                return Results.BadRequest(new { error = "GameId and AgentId are required" });
            }

            try
            {
                var chat = await chatService.CreateChatAsync(session.User.Id, request.GameId, request.AgentId, ct);

                // Reload with navigations
                var fullChat = await chatService.GetChatByIdAsync(chat.Id, session.User.Id, ct);
                if (fullChat == null)
                {
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
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
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Invalid chat creation request");
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var deleted = await chatService.DeleteChatAsync(chatId, session.User.Id, ct);
                if (!deleted)
                {
                    return Results.NotFound(new { error = "Chat not found" });
                }

                logger.LogInformation("User {UserId} deleted chat {ChatId}", session.User.Id, chatId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized chat deletion attempt");
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
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

                try
                {
                    logger.LogInformation("User {UserId} updating message {MessageId} in chat {ChatId}", session.User.Id, messageId, chatId);
                    var updatedMessage = await chatService.UpdateMessageAsync(chatId, messageId, request.Content, session.User.Id, ct);

                    var response = MapToChatMessageResponse(updatedMessage);
                    logger.LogInformation("Message {MessageId} updated successfully by user {UserId}", messageId, session.User.Id);
                    return Results.Ok(response);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning("Message {MessageId} not found in chat {ChatId}: {Error}", messageId, chatId, ex.Message);
                    return Results.NotFound(new { error = "message_not_found", message = ex.Message });
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogWarning("User {UserId} not authorized to update message {MessageId}: {Error}", session.User.Id, messageId, ex.Message);
                    return Results.Problem(statusCode: 403, detail: ex.Message, title: "Forbidden");
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning("Invalid operation updating message {MessageId}: {Error}", messageId, ex.Message);
                    return Results.BadRequest(new { error = "invalid_operation", message = ex.Message });
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: API endpoint boundary - must catch all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (ChatService)
                catch (Exception ex)
                {
                    // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                    // Specific exception handling occurs in service layer (ChatService)
                    logger.LogError(ex, "Error updating message {MessageId} in chat {ChatId}", messageId, chatId);
                    return Results.Problem(statusCode: 500, detail: "An error occurred while updating the message", title: "Internal Server Error");
                }
#pragma warning restore CA1031
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

                try
                {
                    logger.LogInformation("User {UserId} deleting message {MessageId} in chat {ChatId} (admin: {IsAdmin})", session.User.Id, messageId, chatId, isAdmin);
                    var deleted = await chatService.DeleteMessageAsync(chatId, messageId, session.User.Id, isAdmin, ct);

                    if (!deleted)
                    {
                        logger.LogInformation("Message {MessageId} already deleted", messageId);
                        return Results.Ok(new { message = "Message already deleted" });
                    }

                    logger.LogInformation("Message {MessageId} deleted successfully by user {UserId}", messageId, session.User.Id);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning("Message {MessageId} not found in chat {ChatId}: {Error}", messageId, chatId, ex.Message);
                    return Results.NotFound(new { error = "message_not_found", message = ex.Message });
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogWarning("User {UserId} not authorized to delete message {MessageId}: {Error}", session.User.Id, messageId, ex.Message);
                    return Results.Problem(statusCode: 403, detail: ex.Message, title: "Forbidden");
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: API endpoint boundary - must catch all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (ChatService)
                catch (Exception ex)
                {
                    // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                    // Specific exception handling occurs in service layer (ChatService)
                    logger.LogError(ex, "Error deleting message {MessageId} in chat {ChatId}", messageId, chatId);
                    return Results.Problem(statusCode: 500, detail: "An error occurred while deleting the message", title: "Internal Server Error");
                }
#pragma warning restore CA1031
            })
            .RequireAuthorization()
            .WithName("DeleteChatMessage")
            .WithDescription("Soft-delete a message. Users can delete their own messages; admins can delete any message. Invalidates subsequent AI responses. Requires authentication.")
            .WithTags("Chat");

        // CHAT-05: Export chat endpoint
        group.MapPost("/chats/{chatId:guid}/export", async (
            Guid chatId,
            ExportChatRequest request,
            IChatExportService exportService,
            IFeatureFlagService featureFlags,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // CONFIG-05: Check if chat export feature is enabled
            if (!await featureFlags.IsEnabledAsync("Features.ChatExport"))
            {
                return Results.Json(
                    new { error = "feature_disabled", message = "Chat export is currently unavailable", featureName = "Features.ChatExport" },
                    statusCode: 403);
            }

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            try
            {
                var result = await exportService.ExportChatAsync(
                    chatId,
                    userId,
                    request.Format,
                    request.DateFrom,
                    request.DateTo,
                    ct);

                if (!result.Success)
                {
                    return result.Error switch
                    {
                        "not_found" => Results.NotFound(new { error = result.ErrorDetails }),
                        "unsupported_format" => Results.BadRequest(new { error = result.ErrorDetails }),
                        "generation_failed" => Results.Problem(
                            detail: result.ErrorDetails,
                            statusCode: StatusCodes.Status500InternalServerError),
                        _ => Results.Problem("Unknown error occurred during export")
                    };
                }

                logger.LogInformation("User {UserId} exported chat {ChatId} in {Format} format",
                    session.User.Id, chatId, request.Format);

                // Return stream with proper content disposition for download
                return Results.Stream(
                    result.Stream!,
                    contentType: result.ContentType,
                    fileDownloadName: result.Filename,
                    enableRangeProcessing: false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return HTTP 500
            // Specific exception handling occurs in service layer (ChatExportService)
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (ChatExportService)
                logger.LogError(ex, "Unexpected error during chat export for user {UserId}, chat {ChatId}",
                    session.User.Id, chatId);
                return Results.Problem("An unexpected error occurred during export");
            }
#pragma warning restore CA1031
        });

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
