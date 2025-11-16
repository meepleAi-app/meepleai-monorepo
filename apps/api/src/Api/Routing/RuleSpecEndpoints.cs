using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
            var ruleSpec = await mediator.Send(new GetRuleSpecQuery(gameId), ct);

            if (ruleSpec == null)
            {
                logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
                return Results.NotFound(new { error = "RuleSpec not found" });
            }

            return Results.Json(ruleSpec);
        });

        group.MapPut("/games/{gameId:guid}/rulespec", async (Guid gameId, RuleSpec ruleSpec, HttpContext context, IMediator mediator, AuditService auditService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (!string.Equals(ruleSpec.gameId, gameId.ToString(), StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
            }

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            try
            {
                logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", userId, gameId);

                // Convert Model to Command
                var command = new UpdateRuleSpecCommand(
                    GameId: gameId,
                    Version: ruleSpec.version,
                    Atoms: ruleSpec.rules.Select(r => new RuleAtomDto(r.id, r.text, r.section, r.page, r.line)).ToList(),
                    UserId: userId
                );

                var updated = await mediator.Send(command, ct);
                logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.Version);

                // Audit trail
                await auditService.LogAsync(
                    session.User.Id,
                    "UPDATE_RULESPEC",
                    "RuleSpec",
                    gameId.ToString(),
                    "Success",
                    $"Updated RuleSpec to version {updated.Version}",
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString(),
                    ct);

                // Convert DTO back to Model for backward compatibility
                var modelResult = ToModel(updated);
                return Results.Json(modelResult);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Failed to update RuleSpec for game {GameId}: {Error}", gameId, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // RULE-02: Get version history
        group.MapGet("/games/{gameId:guid}/rulespec/history", async (Guid gameId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
            var history = await mediator.Send(new GetVersionHistoryQuery(gameId), ct);
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

            var timeline = await mediator.Send(query, ct);
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
            var ruleSpec = await mediator.Send(new GetRuleSpecVersionQuery(gameId, version), ct);

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
            var diff = await mediator.Send(query, ct);

            if (diff == null)
            {
                return Results.NotFound(new { error = "One or both RuleSpec versions not found" });
            }

            return Results.Json(diff);
        });

        // EDIT-02: RuleSpec comment endpoints
        group.MapPost("/games/{gameId:guid}/rulespec/versions/{version}/comments", async (Guid gameId, string version, CreateRuleSpecCommentRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(request.CommentText))
            {
                return Results.BadRequest(new { error = "CommentText is required" });
            }

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            try
            {
                logger.LogInformation("User {UserId} adding comment to RuleSpec {GameId} version {Version}", userId, gameId, version);
                var command = new CreateSimpleRuleCommentCommand(gameId.ToString(), version, request.AtomId, request.CommentText, userId);
                var comment = await mediator.Send(command, ct);
                logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
                return Results.Created($"/api/v1/games/{gameId}/rulespec/comments/{comment.Id}", comment);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Failed to add comment: {Error}", ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/games/{gameId:guid}/rulespec/versions/{version}/comments", async (Guid gameId, string version, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version}", session!.User.Id, gameId, version);
            var query = new GetSimpleRuleCommentsQuery(gameId.ToString(), version);
            var response = await mediator.Send(query, ct);
            return Results.Json(response);
        });

        group.MapPut("/games/{gameId:guid}/rulespec/comments/{commentId:guid}", async (Guid gameId, Guid commentId, UpdateRuleSpecCommentRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(request.CommentText))
            {
                return Results.BadRequest(new { error = "CommentText is required" });
            }

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            try
            {
                logger.LogInformation("User {UserId} updating comment {CommentId}", userId, commentId);
                var command = new UpdateSimpleRuleCommentCommand(commentId, request.CommentText, userId);
                var comment = await mediator.Send(command, ct);
                logger.LogInformation("Comment {CommentId} updated successfully", commentId);
                return Results.Json(comment);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Failed to update comment {CommentId}: {Error}", commentId, ex.Message);
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning("User {UserId} not authorized to update comment {CommentId}: {Error}", userId, commentId, ex.Message);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
        });

        group.MapDelete("/games/{gameId:guid}/rulespec/comments/{commentId:guid}", async (Guid gameId, Guid commentId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

            try
            {
                logger.LogInformation("User {UserId} deleting comment {CommentId}", userId, commentId);
                var command = new DeleteSimpleRuleCommentCommand(commentId, userId, isAdmin);
                var deleted = await mediator.Send(command, ct);

                if (!deleted)
                {
                    return Results.NotFound(new { error = "Comment not found" });
                }

                logger.LogInformation("Comment {CommentId} deleted successfully", commentId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning("User {UserId} not authorized to delete comment {CommentId}: {Error}", userId, commentId, ex.Message);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            try
            {
                logger.LogInformation("User {UserId} creating comment on RuleSpec {GameId} version {Version}", userId, gameId, version);
                var command = new CreateRuleCommentCommand(gameId, version, request.LineNumber, request.CommentText, userId);
                var comment = await mediator.Send(command, ct);
                logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
                return Results.Created($"/api/v1/comments/{comment.Id}", comment);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 400 response
            // All business exceptions are handled in RuleCommentService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
                // Specific exception handling occurs in service layer (RuleSpecService)
                logger.LogError(ex, "Failed to create comment on RuleSpec {GameId} version {Version}", gameId, version);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning restore CA1031
        })
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            try
            {
                logger.LogInformation("User {UserId} replying to comment {CommentId}", userId, commentId);
                var command = new ReplyToRuleCommentCommand(commentId, request.CommentText, userId);
                var reply = await mediator.Send(command, ct);
                logger.LogInformation("Reply {ReplyId} created successfully", reply.Id);
                return Results.Created($"/api/v1/comments/{reply.Id}", reply);
            }
            catch (InvalidOperationException ex)
            {
                // Handlers throw InvalidOperationException for not found and other business logic errors
                logger.LogWarning("Failed to create reply to comment {CommentId}: {Error}", commentId, ex.Message);

                // Return 404 if the error indicates "not found", otherwise 400
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 400 response
            // All business exceptions are handled in RuleCommentService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
                // Specific exception handling occurs in service layer (RuleSpecService)
                logger.LogError(ex, "Unexpected error creating reply to comment {CommentId}", commentId);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning restore CA1031
        })
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;
            var userId = session!.User.Id;

            logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} (includeResolved: {IncludeResolved})",
                userId, gameId, version, includeResolved);

            var query = new GetRuleCommentsQuery(gameId, version, includeResolved);
            var comments = await mediator.Send(query, ct);
            return Results.Ok(comments);
        })
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;
            var userId = session!.User.Id;

            logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} line {LineNumber}",
                userId, gameId, version, lineNumber);

            var query = new GetCommentsForLineQuery(gameId, version, lineNumber);
            var comments = await mediator.Send(query, ct);
            return Results.Ok(comments);
        })
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

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            try
            {
                logger.LogInformation("User {UserId} resolving comment {CommentId} (resolveReplies: {ResolveReplies})",
                    userId, commentId, resolveReplies);

                var isAdmin = session!.User.Role == "admin";
                var command = new ResolveRuleCommentCommand(commentId, userId, isAdmin, resolveReplies);
                var comment = await mediator.Send(command, ct);
                logger.LogInformation("Comment {CommentId} resolved successfully", commentId);
                return Results.Ok(comment);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Comment {CommentId} not found for resolution", commentId);
                return Results.NotFound(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 400 response
            // All business exceptions are handled in RuleCommentService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
                // Specific exception handling occurs in service layer (RuleSpecService)
                logger.LogError(ex, "Failed to resolve comment {CommentId}", commentId);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning restore CA1031
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

            if (!Guid.TryParse(session!.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            try
            {
                logger.LogInformation("User {UserId} unresolving comment {CommentId} (unresolveParent: {UnresolveParent})",
                    userId, commentId, unresolveParent);

                var isAdmin = session!.User.Role == "admin";
                var command = new UnresolveRuleCommentCommand(commentId, userId, isAdmin, unresolveParent);
                var comment = await mediator.Send(command, ct);
                logger.LogInformation("Comment {CommentId} unresolved successfully", commentId);
                return Results.Ok(comment);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Comment {CommentId} not found for unresolve", commentId);
                return Results.NotFound(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 400 response
            // All business exceptions are handled in RuleCommentService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
                // Specific exception handling occurs in service layer (RuleSpecService)
                logger.LogError(ex, "Failed to unresolve comment {CommentId}", commentId);
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning restore CA1031
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
                return Results.BadRequest(new { error = "At least one rule spec ID must be provided" });
            }

            try
            {
                logger.LogInformation("User {UserId} exporting {Count} rule specs", session!.User.Id, request.RuleSpecIds.Count);

                // Convert List<string> to List<Guid>
                var gameIds = new List<Guid>();
                foreach (var id in request.RuleSpecIds)
                {
                    if (!Guid.TryParse(id, out var guid))
                    {
                        return Results.BadRequest(new { error = $"Invalid rule spec ID format: {id}" });
                    }
                    gameIds.Add(guid);
                }

                var command = new ExportRuleSpecsCommand(gameIds);
                var zipBytes = await mediator.Send(command, ct);

                var fileName = $"meepleai-rulespecs-{DateTime.UtcNow:yyyy-MM-dd}.zip";
                logger.LogInformation("Successfully created ZIP archive {FileName} with {Size} bytes", fileName, zipBytes.Length);

                return Results.File(zipBytes, "application/zip", fileName);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Invalid export request: {Error}", ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Export operation failed: {Error}", ex.Message);
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in Command Handler; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                logger.LogError(ex, "Unexpected error during rule spec export");
                return Results.Problem("An error occurred during export", statusCode: StatusCodes.Status500InternalServerError);
            }
#pragma warning restore CA1031
        });

        return group;
    }

    /// <summary>
    /// Converts RuleSpecDto to legacy RuleSpec Model for backward compatibility.
    /// </summary>
    private static RuleSpec ToModel(RuleSpecDto dto)
    {
        var atoms = dto.Atoms.Select(a => new RuleAtom(a.Id, a.Text, a.Section, a.Page, a.Line)).ToList();
        return new RuleSpec(dto.GameId.ToString(), dto.Version, dto.CreatedAt, atoms);
    }
}