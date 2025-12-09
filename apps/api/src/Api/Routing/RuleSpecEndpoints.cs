using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Routing;

/// <summary>
/// RuleSpec management endpoints.
/// Handles rule specification CRUD, versioning, comments, diffs, and bulk operations.
/// </summary>
public static class RuleSpecEndpoints
{
    public static RouteGroupBuilder MapRuleSpecEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId:guid}/rulespec", async (Guid gameId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
            var ruleSpec = await mediator.Send(new GetRuleSpecQuery(gameId), ct).ConfigureAwait(false);

            if (ruleSpec == null)
            {
                logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
                return Results.NotFound(new { error = "RuleSpec not found" });
            }

            return Results.Json(ruleSpec);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapPut("/games/{gameId:guid}/rulespec", async (Guid gameId, RuleSpec ruleSpec, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (!string.Equals(ruleSpec.gameId, gameId.ToString(), StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
            }

            var userId = session!.User!.Id;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", userId, gameId);

            // Convert Model to Command (including audit context)
            var command = new UpdateRuleSpecCommand(
                GameId: gameId,
                Version: ruleSpec.version,
                Atoms: ruleSpec.rules.Select(r => new RuleAtomDto(r.id, r.text, r.section, r.page, r.line)).ToList(),
                UserId: userId,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString()
            );

            var updated = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.Version);

            // Issue #1676 Phase 2: Return RuleSpecDto directly (no legacy conversion)
            return Results.Json(updated);
        });

        // RULE-02: Get version history
        group.MapGet("/games/{gameId:guid}/rulespec/history", async (Guid gameId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
            var history = await mediator.Send(new GetVersionHistoryQuery(gameId), ct).ConfigureAwait(false);
            return Results.Json(history);
        });

        // EDIT-06: Get version timeline with filters
        group.MapGet("/games/{gameId}/rulespec/versions/timeline", async (
            string gameId,
            DateTime? startDate,
            DateTime? endDate,
            string? author,
            string? searchQuery,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (!Guid.TryParse(gameId, out var gameGuid))
            {
                return Results.BadRequest(new { error = "Invalid game ID format" });
            }

            logger.LogInformation("Fetching RuleSpec version timeline for game {GameId}", gameId);

            var query = new GetVersionTimelineQuery(
                GameId: gameGuid,
                StartDate: startDate,
                EndDate: endDate,
                Author: author,
                SearchQuery: searchQuery
            );

            var timeline = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(timeline);
        })
        .RequireAuthorization()
        .WithName("GetVersionTimeline")
        .WithTags("Versions");

        // RULE-02: Get specific version
        group.MapGet("/games/{gameId:guid}/rulespec/versions/{version}", async (Guid gameId, string version, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
            var ruleSpec = await mediator.Send(new GetRuleSpecVersionQuery(gameId, version), ct).ConfigureAwait(false);

            if (ruleSpec == null)
            {
                logger.LogInformation("RuleSpec version {Version} not found for game {GameId}", version, gameId);
                return Results.NotFound(new { error = "RuleSpec version not found" });
            }

            return Results.Json(ruleSpec);
        });

        // RULE-02: Compare two versions (diff)
        group.MapGet("/games/{gameId:guid}/rulespec/diff", async (Guid gameId, string? from, string? to, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                return Results.BadRequest(new { error = "Both 'from' and 'to' version parameters are required" });
            }

            logger.LogInformation("Computing diff between versions {FromVersion} and {ToVersion} for game {GameId}", from, to, gameId);

            var query = new ComputeRuleSpecDiffQuery(gameId, from, to);
            var diff = await mediator.Send(query, ct).ConfigureAwait(false);

            if (diff == null)
            {
                return Results.NotFound(new { error = "One or both RuleSpec versions not found" });
            }

            return Results.Json(diff);
        });

        // EDIT-05: Enhanced Comments System endpoints
        // 1. Create top-level comment
        group.MapPost("/rulespecs/{gameId}/{version}/comments", async (
            string gameId,
            string version,
            CreateCommentRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var userId = session!.User!.Id;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} creating comment on RuleSpec {GameId} version {Version}", userId, gameId, version);
            var command = new CreateRuleCommentCommand(gameId, version, request.LineNumber, request.CommentText, userId);
            var comment = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
            return Results.Created($"/api/v1/comments/{comment.Id}", comment);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("CreateRuleSpecComment")
        .WithTags("Comments")
        .WithDescription("Create a top-level comment on a rule specification version")
        .Produces<RuleCommentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. Create reply to comment
        group.MapPost("/comments/{commentId}/replies", async (
            Guid commentId,
            CreateReplyRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var userId = session!.User!.Id;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} replying to comment {CommentId}", userId, commentId);
            var command = new ReplyToRuleCommentCommand(commentId, request.CommentText, userId);
            var reply = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Reply {ReplyId} created successfully", reply.Id);
            return Results.Created($"/api/v1/comments/{reply.Id}", reply);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("CreateCommentReply")
        .WithTags("Comments")
        .WithDescription("Create a threaded reply to an existing comment")
        .Produces<RuleCommentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 3. Get all comments for RuleSpec
        group.MapGet("/rulespecs/{gameId}/{version}/comments", async (
            string gameId,
            string version,
            bool includeResolved,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} (includeResolved: {IncludeResolved})",
                userId, gameId, version, includeResolved);

            var query = new GetRuleCommentsQuery(gameId, version, includeResolved);
            var comments = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(comments);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("GetRuleSpecComments")
        .WithTags("Comments")
        .WithDescription("Get all comments for a specific rule specification version (hierarchical structure)")
        .Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 4. Get comments for specific line
        group.MapGet("/rulespecs/{gameId}/{version}/lines/{lineNumber}/comments", async (
            string gameId,
            string version,
            int lineNumber,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} line {LineNumber}",
                userId, gameId, version, lineNumber);

            var query = new GetCommentsForLineQuery(gameId, version, lineNumber);
            var comments = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(comments);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .RequireAuthorization()
        .WithName("GetLineComments")
        .WithTags("Comments")
        .WithDescription("Get all comments for a specific line in a rule specification")
        .Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 5. Resolve comment
        group.MapPost("/comments/{commentId}/resolve", async (
            Guid commentId,
            bool resolveReplies,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var userId = session!.User!.Id;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} resolving comment {CommentId} (resolveReplies: {ResolveReplies})",
                userId, commentId, resolveReplies);

            var isAdmin = string.Equals(session!.User.Role, "admin", StringComparison.Ordinal);
            var command = new ResolveRuleCommentCommand(commentId, userId, isAdmin, resolveReplies);
            var comment = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Comment {CommentId} resolved successfully", commentId);
            return Results.Ok(comment);
        })
        .RequireAuthorization()
        .WithName("ResolveComment")
        .WithTags("Comments")
        .WithDescription("Mark a comment as resolved, optionally resolving all child replies (Admin/Editor only)")
        .Produces<RuleCommentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 6. Unresolve comment
        group.MapPost("/comments/{commentId}/unresolve", async (
            Guid commentId,
            bool unresolveParent,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var userId = session!.User!.Id;

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} unresolving comment {CommentId} (unresolveParent: {UnresolveParent})",
                userId, commentId, unresolveParent);

            var isAdmin = string.Equals(session!.User.Role, "admin", StringComparison.Ordinal);
            var command = new UnresolveRuleCommentCommand(commentId, userId, isAdmin, unresolveParent);
            var comment = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Comment {CommentId} unresolved successfully", commentId);
            return Results.Ok(comment);
        })
        .RequireAuthorization()
        .WithName("UnresolveComment")
        .WithTags("Comments")
        .WithDescription("Reopen a resolved comment, optionally unresolving parent if this is a reply (Admin/Editor only)")
        .Produces<RuleCommentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // EDIT-07: Bulk RuleSpec operations
        group.MapPost("/rulespecs/bulk/export", async (BulkExportRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (request?.RuleSpecIds == null || request.RuleSpecIds.Count == 0)
            {
                throw new BadRequestException("At least one rule spec ID must be provided");
            }

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            logger.LogInformation("User {UserId} exporting {Count} rule specs", session!.User!.Id, request.RuleSpecIds.Count);

            // Convert List<string> to List<Guid>
            var gameIds = new List<Guid>();
            foreach (var id in request.RuleSpecIds)
            {
                if (!Guid.TryParse(id, out var guid))
                {
                    throw new BadRequestException($"Invalid rule spec ID format: {id}");
                }
                gameIds.Add(guid);
            }

            var command = new ExportRuleSpecsCommand(gameIds);
            var zipBytes = await mediator.Send(command, ct).ConfigureAwait(false);

            var fileName = $"meepleai-rulespecs-{DateTime.UtcNow:yyyy-MM-dd}.zip";
            logger.LogInformation("Successfully created ZIP archive {FileName} with {Size} bytes", fileName, zipBytes.Length);

            return Results.File(zipBytes, "application/zip", fileName);
        });

        return group;
    }

    // Issue #1676 Phase 2: ToModel() helper removed (no longer needed, return RuleSpecDto directly)
}