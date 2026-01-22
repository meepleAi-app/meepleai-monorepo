using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Session management endpoints (Admin only).
/// Handles session retrieval, revocation, and bulk operations.
/// </summary>
internal static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        // AUTH-03: Session management endpoints
        group.MapGet("/admin/sessions", async (HttpContext context, IMediator mediator, int limit = 100, string? userId = null, CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetAllSessionsQuery(
                UserId: string.IsNullOrEmpty(userId) ? null : Guid.Parse(userId),
                Limit: limit
            );
            var sessions = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(sessions);
        })
        .Produces<List<Api.Models.SessionInfo>>(200)
        .Produces(401)
        .Produces(403)
        .WithTags("Admin", "Sessions")
        .WithSummary("Get all user sessions")
        .WithDescription("Retrieves a list of all active and revoked sessions. Admin only. Supports optional filtering by userId and pagination via limit parameter.");

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
        })
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Admin", "Sessions")
        .WithSummary("Revoke a single session")
        .WithDescription("Revokes a specific user session by ID. Admin only. The session will be immediately invalidated and the user will be logged out.");

        group.MapDelete("/admin/users/{userId:guid}/sessions", async (Guid userId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {AdminId} revoking all sessions for user {UserId}", session!.User!.Id, userId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RevokeAllUserSessionsCommand(userId);
            var count = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
            return Results.Json(new { ok = true, revokedCount = count });
        })
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .WithTags("Admin", "Sessions")
        .WithSummary("Revoke all sessions for a user")
        .WithDescription("Revokes all active sessions for a specific user. Admin only. Use this to force logout a user across all devices.");

        return group;
    }
}
