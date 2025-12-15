using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;
using Api.Extensions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// ISSUE-2052: Shareable chat thread link endpoints.
/// Provides functionality to create, revoke, and access shared chat threads.
/// </summary>
internal static class ShareLinkEndpoints
{
    public static RouteGroupBuilder MapShareLinkEndpoints(this RouteGroupBuilder group)
    {
        // 1. Create shareable link (authenticated)
        group.MapPost("/share-links", async (
            CreateShareLinkRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Validate request
            if (!Guid.TryParse(req.ThreadId, out var threadId))
            {
                return Results.BadRequest(new { error = "Invalid threadId format" });
            }

            if (!Enum.TryParse<ShareLinkRole>(req.Role, ignoreCase: true, out var role))
            {
                return Results.BadRequest(new { error = "Invalid role. Must be 'view' or 'comment'" });
            }

            // Parse expiry duration
            var expiresAt = DateTime.UtcNow.AddDays(req.ExpiryDays ?? 7);

            logger.LogInformation(
                "Creating share link for thread {ThreadId} with role {Role} by user {UserId}",
                threadId, role, session.User!.Id);

            var command = new CreateShareLinkCommand(
                ThreadId: threadId,
                Role: role,
                ExpiresAt: expiresAt,
                Label: req.Label,
                UserId: session.User.Id
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Share link created: {ShareLinkId}, expires at {ExpiresAt}",
                result.ShareLinkId, result.ExpiresAt);

            return Results.Ok(new
            {
                shareLinkId = result.ShareLinkId,
                token = result.Token,
                shareableUrl = result.ShareableUrl,
                expiresAt = result.ExpiresAt,
                role = role.ToString().ToLowerInvariant()
            });
        })
        .WithName("CreateShareLink")
        .RequireSession() // ISSUE-1446: Automatic session validation
        .WithTags("ShareLinks");

        // 2. Revoke shareable link (authenticated)
        group.MapDelete("/share-links/{id}", async (
            string id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            if (!Guid.TryParse(id, out var shareLinkId))
            {
                return Results.BadRequest(new { error = "Invalid share link ID format" });
            }

            logger.LogInformation(
                "Revoking share link {ShareLinkId} by user {UserId}",
                shareLinkId, session.User!.Id);

            var command = new RevokeShareLinkCommand(
                ShareLinkId: shareLinkId,
                UserId: session.User.Id
            );

            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!success)
            {
                logger.LogWarning(
                    "Failed to revoke share link {ShareLinkId}: not found or unauthorized",
                    shareLinkId);
                return Results.NotFound(new { error = "Share link not found or unauthorized" });
            }

            logger.LogInformation("Share link {ShareLinkId} revoked successfully", shareLinkId);

            return Results.Ok(new { success = true });
        })
        .WithName("RevokeShareLink")
        .RequireSession() // ISSUE-1446: Automatic session validation
        .WithTags("ShareLinks");

        // 3. Get shared thread (public, no session required)
        group.MapGet("/shared/thread", async (
            string? token,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new { error = "Token is required" });
            }

            logger.LogInformation("Retrieving shared thread via token");

            var query = new GetSharedThreadQuery(Token: token);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                logger.LogWarning("Shared thread not found or token invalid");
                return Results.NotFound(new { error = "Thread not found or access denied" });
            }

            logger.LogInformation(
                "Shared thread {ThreadId} retrieved with role {Role}",
                result.ThreadId, result.Role);

            return Results.Ok(new
            {
                threadId = result.ThreadId,
                title = result.Title,
                messages = result.Messages,
                role = result.Role.ToString().ToLowerInvariant(),
                gameId = result.GameId,
                createdAt = result.CreatedAt,
                lastMessageAt = result.LastMessageAt
            });
        })
        .WithName("GetSharedThread")
        .AllowAnonymous() // Public access via JWT token
        .WithTags("ShareLinks", "Public");

        // 4. Add comment to shared thread (public, requires comment role)
        group.MapPost("/shared/thread/comment", async (
            AddCommentRequest req,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token))
            {
                return Results.BadRequest(new { error = "Token is required" });
            }

            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            logger.LogInformation("Adding comment to shared thread via token");

            var command = new AddCommentToSharedThreadCommand(
                Token: req.Token,
                Content: req.Content
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);

                if (result == null)
                {
                    logger.LogWarning("Failed to add comment: invalid or revoked token");
                    return Results.Unauthorized();
                }

                logger.LogInformation("Comment added to shared thread: message {MessageId}", result.MessageId);

                return Results.Ok(new
                {
                    messageId = result.MessageId,
                    timestamp = result.Timestamp
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("comment permissions"))
            {
                logger.LogWarning("Comment denied: insufficient permissions");
                return Results.StatusCode(403); // Forbidden
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
            {
                logger.LogWarning("Comment denied: rate limit exceeded");
                return Results.StatusCode(429); // Too Many Requests
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Comment validation failed: {Error}", ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddCommentToSharedThread")
        .AllowAnonymous() // Public access via JWT token
        .WithTags("ShareLinks", "Public");

        return group;
    }
}

/// <summary>
/// Request payload for creating a share link.
/// </summary>
internal record CreateShareLinkRequest(
    string ThreadId,
    string Role,
    int? ExpiryDays,
    string? Label
);

/// <summary>
/// Request payload for adding a comment to shared thread.
/// </summary>
internal record AddCommentRequest(
    string Token,
    string Content
);
