using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Endpoints for GDPR Art. 17 right to erasure of LLM-related user data.
/// Issue #5509: Delete all LLM request logs, conversation memories, and Redis keys for a user.
/// </summary>
internal static class UserLlmDataEndpoints
{
    public static RouteGroupBuilder MapUserLlmDataEndpoints(this RouteGroupBuilder group)
    {
        MapDeleteSelfLlmData(group);
        MapAdminDeleteUserLlmData(group);

        return group;
    }

    /// <summary>
    /// Self-service: authenticated user deletes their own LLM data.
    /// </summary>
    private static void MapDeleteSelfLlmData(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me/llm-data", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            var command = new DeleteUserLlmDataCommand(
                UserId: userId,
                RequestedByUserId: userId,
                IsAdminRequest: false);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogWarning(
                "GDPR self-service erasure completed: UserId={UserId}, Logs={Logs}, Memories={Memories}, Redis={Redis}",
                userId, result.LlmRequestLogsDeleted, result.ConversationMemoriesDeleted, result.RedisKeysCleared);

            return Results.Json(new
            {
                ok = true,
                message = "LLM data deleted successfully",
                llmRequestLogsDeleted = result.LlmRequestLogsDeleted,
                conversationMemoriesDeleted = result.ConversationMemoriesDeleted,
                redisKeysCleared = result.RedisKeysCleared,
                deletedAt = result.DeletedAt
            });
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("DeleteSelfLlmData")
        .WithTags("User Profile", "GDPR")
        .WithSummary("Delete all LLM-related data for the authenticated user")
        .WithDescription(@"GDPR Art. 17 right to erasure: deletes all LLM request logs,
conversation memories, and Redis session keys for the current user.

**Data stores affected**: LlmRequestLog (DB), ConversationMemory (DB), Redis user keys.
**JSONL logs**: Already hash UserIds (no action needed — auto-expire in 30 days).")
        .Produces(200)
        .Produces(401);
    }

    /// <summary>
    /// Admin: delete LLM data for any user by ID.
    /// </summary>
    private static void MapAdminDeleteUserLlmData(RouteGroupBuilder group)
    {
        group.MapDelete("/users/{userId:guid}/llm-data", async (
            Guid userId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized)
                return error!;

            var command = new DeleteUserLlmDataCommand(
                UserId: userId,
                RequestedByUserId: session!.User!.Id,
                IsAdminRequest: true);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogWarning(
                "GDPR admin erasure completed: TargetUserId={TargetUserId}, AdminUserId={AdminUserId}, Logs={Logs}, Memories={Memories}, Redis={Redis}",
                userId, session.User.Id, result.LlmRequestLogsDeleted, result.ConversationMemoriesDeleted, result.RedisKeysCleared);

            return Results.Json(new
            {
                ok = true,
                message = "LLM data deleted successfully",
                llmRequestLogsDeleted = result.LlmRequestLogsDeleted,
                conversationMemoriesDeleted = result.ConversationMemoriesDeleted,
                redisKeysCleared = result.RedisKeysCleared,
                deletedAt = result.DeletedAt
            });
        })
        .RequireAuthorization()
        .WithName("AdminDeleteUserLlmData")
        .WithTags("Admin", "GDPR")
        .WithSummary("Admin: Delete all LLM-related data for a specific user")
        .WithDescription(@"GDPR Art. 17 right to erasure (admin-initiated): deletes all LLM request logs,
conversation memories, and Redis session keys for the specified user.

**Requires**: Admin session.
**Audit**: Logs admin user ID and target user ID for compliance.")
        .Produces(200)
        .Produces(401)
        .Produces(403);
    }
}
