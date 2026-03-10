using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Endpoints for GDPR Art. 17 right to erasure — full account deletion.
/// </summary>
internal static class UserAccountEndpoints
{
    public static RouteGroupBuilder MapUserAccountEndpoints(this RouteGroupBuilder group)
    {
        MapDeleteOwnAccount(group);
        MapExportOwnData(group);

        return group;
    }

    /// <summary>
    /// Self-service: authenticated user deletes their own account and all associated data.
    /// </summary>
    private static void MapDeleteOwnAccount(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            var command = new DeleteOwnAccountCommand(UserId: userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogWarning(
                "GDPR Art.17: Account deleted (self-service). UserId={UserId}, Sessions={Sessions}, LlmLogs={LlmLogs}, Memories={Memories}",
                userId, result.SessionsRevoked, result.LlmRequestLogsDeleted, result.ConversationMemoriesDeleted);

            return Results.Json(new
            {
                ok = true,
                message = "Account and all associated data deleted successfully",
                deletedAt = result.DeletedAt
            });
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("DeleteOwnAccount")
        .WithTags("User Profile", "GDPR")
        .WithSummary("Delete own account and all associated data (GDPR Art. 17)")
        .WithDescription(@"GDPR Art. 17 right to erasure: permanently deletes the user account
and cascades deletion to all associated data including sessions, API keys,
OAuth accounts, backup codes, LLM request logs, conversation memories, and Redis keys.

**Irreversible**: This action cannot be undone.
**Last admin protection**: The last admin account cannot be deleted.")
        .Produces(200)
        .Produces(401)
        .Produces(409);
    }

    /// <summary>
    /// Self-service: authenticated user exports all their data (GDPR Art. 20 data portability).
    /// </summary>
    private static void MapExportOwnData(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/export", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            var command = new ExportUserDataCommand(UserId: userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "GDPR Art.20: Data export requested. UserId={UserId}, Games={Games}, Threads={Threads}",
                userId, result.Summary.TotalLibraryGames, result.Summary.TotalChatThreads);

            return Results.Json(result);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("ExportOwnData")
        .WithTags("User Profile", "GDPR")
        .WithSummary("Export all personal data (GDPR Art. 20 data portability)")
        .WithDescription(@"GDPR Art. 20 right to data portability: exports all user data in a
structured, machine-readable JSON format including profile, preferences,
game library, chat threads, notifications, and AI consent records.")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }
}
