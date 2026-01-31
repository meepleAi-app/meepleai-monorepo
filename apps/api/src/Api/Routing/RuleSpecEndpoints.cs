using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// RuleSpec management endpoints.
/// Handles rule specification CRUD, versioning, comments, diffs, and bulk operations.
/// </summary>
internal static class RuleSpecEndpoints
{
    public static RouteGroupBuilder MapRuleSpecEndpoints(this RouteGroupBuilder group)
    {
        MapCoreRuleSpecEndpoints(group);
        MapVersioningEndpoints(group);
        MapCommentEndpoints(group);
        MapBulkOperationsEndpoints(group);
        MapLockingEndpoints(group);

        return group;
    }

    private static void MapCoreRuleSpecEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId:guid}/rulespec", HandleGetRuleSpec)
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapPut("/games/{gameId:guid}/rulespec", HandleUpdateRuleSpec);
    }

    private static void MapVersioningEndpoints(RouteGroupBuilder group)
    {
        // RULE-02: Get version history
        group.MapGet("/games/{gameId:guid}/rulespec/history", HandleGetVersionHistory);

        // EDIT-06: Get version timeline with filters
        group.MapGet("/games/{gameId}/rulespec/versions/timeline", HandleGetVersionTimeline)
        .RequireAuthorization()
        .WithName("GetVersionTimeline")
        .WithTags("Versions");

        // RULE-02: Get specific version
        group.MapGet("/games/{gameId:guid}/rulespec/versions/{version}", HandleGetVersion);

        // RULE-02: Compare two versions (diff)
        group.MapGet("/games/{gameId:guid}/rulespec/diff", HandleGetDiff);
    }

    private static void MapCommentEndpoints(RouteGroupBuilder group)
    {
        MapCommentCreationEndpoints(group);
        MapCommentRetrievalEndpoints(group);
        MapCommentResolutionEndpoints(group);
        MapCommentModificationEndpoints(group);
    }

    private static void MapCommentCreationEndpoints(RouteGroupBuilder group)
    {
        // 1. Create top-level comment
        group.MapPost("/rulespecs/{gameId}/{version}/comments", HandleCreateComment)
        .RequireSession()
        .RequireAuthorization()
        .WithName("CreateRuleSpecComment")
        .WithTags("Comments")
        .WithDescription("Create a top-level comment on a rule specification version")
        .Produces<RuleCommentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. Create reply to comment
        group.MapPost("/comments/{commentId}/replies", HandleCreateReply)
        .RequireSession()
        .RequireAuthorization()
        .WithName("CreateCommentReply")
        .WithTags("Comments")
        .WithDescription("Create a threaded reply to an existing comment")
        .Produces<RuleCommentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapCommentRetrievalEndpoints(RouteGroupBuilder group)
    {
        // 3. Get all comments for RuleSpec
        group.MapGet("/rulespecs/{gameId}/{version}/comments", HandleGetComments)
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetRuleSpecComments")
        .WithTags("Comments")
        .WithDescription("Get all comments for a specific rule specification version (hierarchical structure)")
        .Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 4. Get comments for specific line
        group.MapGet("/rulespecs/{gameId}/{version}/lines/{lineNumber}/comments", HandleGetLineComments)
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetLineComments")
        .WithTags("Comments")
        .WithDescription("Get all comments for a specific line in a rule specification")
        .Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static void MapCommentResolutionEndpoints(RouteGroupBuilder group)
    {
        // 5. Resolve comment
        group.MapPost("/comments/{commentId}/resolve", HandleResolveComment)
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
        group.MapPost("/comments/{commentId}/unresolve", HandleUnresolveComment)
        .RequireAuthorization()
        .WithName("UnresolveComment")
        .WithTags("Comments")
        .WithDescription("Reopen a resolved comment, optionally unresolving parent if this is a reply (Admin/Editor only)")
        .Produces<RuleCommentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapCommentModificationEndpoints(RouteGroupBuilder group)
    {
        // 7. Update comment text
        group.MapPut("/comments/{commentId}", HandleUpdateComment)
        .RequireAuthorization()
        .WithName("UpdateComment")
        .WithTags("Comments")
        .Produces<RuleCommentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 8. Delete comment
        group.MapDelete("/comments/{commentId}", HandleDeleteComment)
        .RequireAuthorization()
        .WithName("DeleteComment")
        .WithTags("Comments")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapBulkOperationsEndpoints(RouteGroupBuilder group)
    {
        // EDIT-07: Bulk RuleSpec operations
        group.MapPost("/rulespecs/bulk/export", HandleBulkExport);
    }

    private static void MapLockingEndpoints(RouteGroupBuilder group)
    {
        MapLockLifecycleEndpoints(group);
        MapLockStateEndpoints(group);
    }

    private static void MapLockLifecycleEndpoints(RouteGroupBuilder group)
    {
        // Issue #2055: Collaborative editing lock endpoints
        // 1. Acquire lock
        group.MapPost("/games/{gameId:guid}/rulespec/lock", HandleAcquireLock)
        .WithName("AcquireEditorLock")
        .WithTags("EditorLocks")
        .WithDescription("Acquire a collaborative editing lock for a RuleSpec");

        // 2. Release lock
        group.MapDelete("/games/{gameId:guid}/rulespec/lock", HandleReleaseLock)
        .WithName("ReleaseEditorLock")
        .WithTags("EditorLocks")
        .WithDescription("Release a collaborative editing lock for a RuleSpec");
    }

    private static void MapLockStateEndpoints(RouteGroupBuilder group)
    {
        // 3. Refresh lock (extend TTL)
        group.MapPost("/games/{gameId:guid}/rulespec/lock/refresh", HandleRefreshLock)
        .WithName("RefreshEditorLock")
        .WithTags("EditorLocks")
        .WithDescription("Refresh (extend) a collaborative editing lock TTL");

        // 4. Get lock status
        group.MapGet("/games/{gameId:guid}/rulespec/lock", HandleGetLockStatus)
        .WithName("GetEditorLockStatus")
        .WithTags("EditorLocks")
        .WithDescription("Get the current lock status for a RuleSpec");
    }

    private static async Task<IResult> HandleGetRuleSpec(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
        var ruleSpec = await mediator.Send(new GetRuleSpecQuery(gameId), ct).ConfigureAwait(false);

        if (ruleSpec == null)
        {
            logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
            return Results.NotFound(new { error = "RuleSpec not found" });
        }

        return Results.Json(ruleSpec);
    }

    private static async Task<IResult> HandleUpdateRuleSpec(
        Guid gameId,
        RuleSpec ruleSpec,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
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
    }

    private static async Task<IResult> HandleGetVersionHistory(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
        var history = await mediator.Send(new GetVersionHistoryQuery(gameId), ct).ConfigureAwait(false);
        return Results.Json(history);
    }

    private static async Task<IResult> HandleGetVersionTimeline(
        string gameId,
        DateTime? startDate,
        DateTime? endDate,
        string? author,
        string? searchQuery,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
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
    }

    private static async Task<IResult> HandleGetVersion(
        Guid gameId,
        string version,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
        var ruleSpec = await mediator.Send(new GetRuleSpecVersionQuery(gameId, version), ct).ConfigureAwait(false);

        if (ruleSpec == null)
        {
            logger.LogInformation("RuleSpec version {Version} not found for game {GameId}", version, gameId);
            return Results.NotFound(new { error = "RuleSpec version not found" });
        }

        return Results.Json(ruleSpec);
    }

    private static async Task<IResult> HandleGetDiff(
        Guid gameId,
        string? from,
        string? to,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminOrEditorSession();
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
    }

    private static async Task<IResult> HandleCreateComment(
        string gameId,
        string version,
        CreateCommentRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} creating comment on RuleSpec {GameId} version {Version}", userId, gameId, version);
        var command = new CreateRuleCommentCommand(gameId, version, request.LineNumber, request.CommentText, userId);
        var comment = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
        return Results.Created($"/api/v1/comments/{comment.Id}", comment);
    }

    private static async Task<IResult> HandleCreateReply(
        Guid commentId,
        CreateReplyRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} replying to comment {CommentId}", userId, commentId);
        var command = new ReplyToRuleCommentCommand(commentId, request.CommentText, userId);
        var reply = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Reply {ReplyId} created successfully", reply.Id);
        return Results.Created($"/api/v1/comments/{reply.Id}", reply);
    }

    private static async Task<IResult> HandleGetComments(
        string gameId,
        string version,
        bool includeResolved,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} (includeResolved: {IncludeResolved})",
            userId, gameId, version, includeResolved);

        var query = new GetRuleCommentsQuery(gameId, version, includeResolved);
        var comments = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(comments);
    }

    private static async Task<IResult> HandleGetLineComments(
        string gameId,
        string version,
        int lineNumber,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} line {LineNumber}",
            userId, gameId, version, lineNumber);

        var query = new GetCommentsForLineQuery(gameId, version, lineNumber);
        var comments = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(comments);
    }

    private static async Task<IResult> HandleResolveComment(
        Guid commentId,
        bool resolveReplies,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;
        logger.LogInformation("User {UserId} resolving comment {CommentId} (resolveReplies: {ResolveReplies})",
            userId, commentId, resolveReplies);

        var isAdmin = string.Equals(session!.User.Role, "admin", StringComparison.Ordinal);
        var command = new ResolveRuleCommentCommand(commentId, userId, isAdmin, resolveReplies);
        var comment = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Comment {CommentId} resolved successfully", commentId);
        return Results.Ok(comment);
    }

    private static async Task<IResult> HandleUnresolveComment(
        Guid commentId,
        bool unresolveParent,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;
        logger.LogInformation("User {UserId} unresolving comment {CommentId} (unresolveParent: {UnresolveParent})",
            userId, commentId, unresolveParent);

        var isAdmin = string.Equals(session!.User.Role, "admin", StringComparison.Ordinal);
        var command = new UnresolveRuleCommentCommand(commentId, userId, isAdmin, unresolveParent);
        var comment = await mediator.Send(command, ct).ConfigureAwait(false);
        logger.LogInformation("Comment {CommentId} unresolved successfully", commentId);
        return Results.Ok(comment);
    }

    private static async Task<IResult> HandleUpdateComment(
        Guid commentId,
        UpdateCommentRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;
        logger.LogInformation("User {UserId} updating comment {CommentId}", userId, commentId);

        var command = new UpdateRuleCommentCommand(commentId, request.CommentText, userId);
        var updated = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(updated);
    }

    private static async Task<IResult> HandleDeleteComment(
        Guid commentId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;
        var isAdmin = string.Equals(session.User.Role, "Admin", StringComparison.Ordinal);
        logger.LogInformation("User {UserId} deleting comment {CommentId}", userId, commentId);

        var command = new DeleteRuleCommentCommand(commentId, userId, isAdmin);
        var deleted = await mediator.Send(command, ct).ConfigureAwait(false);

        return deleted ? Results.NoContent() : Results.NotFound(new { error = "Comment not found" });
    }

    private static async Task<IResult> HandleBulkExport(
        BulkExportRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        if (request?.RuleSpecIds == null || request.RuleSpecIds.Count == 0)
        {
            throw new BadRequestException("At least one rule spec ID must be provided");
        }

        logger.LogInformation("User {UserId} exporting {Count} rule specs", session!.User!.Id, request.RuleSpecIds.Count);

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
    }

    private static async Task<IResult> HandleAcquireLock(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;
        var userEmail = session.User.Email ?? "unknown@user.com";

        logger.LogInformation("User {UserId} attempting to acquire lock for game {GameId}", userId, gameId);

        var command = new AcquireEditorLockCommand(gameId, userId, userEmail);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogInformation("Lock acquisition denied for game {GameId}: {Message}", gameId, result.Message);
            return Results.Conflict(new { error = result.Message, lockStatus = result.LockStatus });
        }

        logger.LogInformation("Lock acquired for game {GameId} by user {UserId}", gameId, userId);
        return Results.Ok(result.LockStatus);
    }

    private static async Task<IResult> HandleReleaseLock(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation("User {UserId} releasing lock for game {GameId}", userId, gameId);

        var command = new ReleaseEditorLockCommand(gameId, userId);
        var released = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!released)
        {
            logger.LogWarning("User {UserId} could not release lock for game {GameId} (not owner)", userId, gameId);
            return Results.BadRequest(new { error = "You do not hold the lock for this RuleSpec" });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> HandleRefreshLock(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        var command = new RefreshEditorLockCommand(gameId, userId);
        var refreshed = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!refreshed)
        {
            logger.LogDebug("Lock refresh failed for game {GameId} by user {UserId}", gameId, userId);
            return Results.BadRequest(new { error = "You do not hold the lock or it has expired" });
        }

        logger.LogDebug("Lock refreshed for game {GameId} by user {UserId}", gameId, userId);
        return Results.Ok(new { message = "Lock refreshed" });
    }

    private static async Task<IResult> HandleGetLockStatus(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
                CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        var query = new GetEditorLockStatusQuery(gameId, userId);
        var status = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(status);
    }

    private sealed record UpdateCommentRequest(string CommentText);
}
