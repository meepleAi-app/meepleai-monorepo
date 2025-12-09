using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Session management endpoints (Admin only).
/// Handles session retrieval, revocation, and bulk operations.
/// </summary>
public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        // AUTH-03: Session management endpoints
        group.MapGet("/admin/sessions", async (HttpContext context, IMediator mediator, int limit = 100, string? userId = null, CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetAllSessionsQuery(
                UserId: string.IsNullOrEmpty(userId) ? null : Guid.Parse(userId),
                Limit: limit
            );
            var sessions = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(sessions);
        });

        group.MapDelete("/admin/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} revoking session {SessionId}", session!.User!.Id, sessionId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RevokeSessionCommand(
                sessionId,
                session!.User!.Id,
                IsRequestingUserAdmin: true,
                Reason: "Admin revocation"
            );
            var response = await mediator.Send(command, ct).ConfigureAwait(false);
            if (!response.Success)
            {
                return Results.NotFound(new { error = response.ErrorMessage ?? "Session not found or already revoked" });
            }

            logger.LogInformation("Session {SessionId} revoked successfully", sessionId);
            return Results.Json(new { ok = true });
        });

        group.MapDelete("/admin/users/{userId:guid}/sessions", async (Guid userId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} revoking all sessions for user {UserId}", session!.User!.Id, userId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RevokeAllUserSessionsCommand(userId);
            var count = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
            return Results.Json(new { ok = true, revokedCount = count });
        });

        return group;
    }
}