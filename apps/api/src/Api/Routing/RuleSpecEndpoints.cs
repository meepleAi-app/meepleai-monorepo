using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
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
group.MapGet("/games/{gameId}/rulespec", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
    var ruleSpec = await ruleSpecService.GetRuleSpecAsync(gameId, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
        return Results.NotFound(new { error = "RuleSpec not found" });
    }

    return Results.Json(ruleSpec);
});

group.MapPut("/games/{gameId}/rulespec", async (string gameId, RuleSpec ruleSpec, HttpContext context, RuleSpecService ruleSpecService, AuditService auditService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to update RuleSpec without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.Equals(ruleSpec.gameId, gameId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", session.User.Id, gameId);
        var updated = await ruleSpecService.UpdateRuleSpecAsync(gameId, ruleSpec, session.User.Id, ct);
        logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.version);

        // Audit trail for RuleSpec changes
        await auditService.LogAsync(
            session.User.Id,
            "UPDATE_RULESPEC",
            "RuleSpec",
            gameId,
            "Success",
            $"Updated RuleSpec to version {updated.version}",
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);

        return Results.Json(updated);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update RuleSpec for game {GameId}: {Error}", gameId, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// RULE-02: Get version history
group.MapGet("/games/{gameId}/rulespec/history", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
    var history = await ruleSpecService.GetVersionHistoryAsync(gameId, ct);
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
    RuleSpecService ruleSpecService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version timeline for game {GameId}", gameId);

    var filters = new VersionTimelineFilters
    {
        StartDate = startDate,
        EndDate = endDate,
        Author = author,
        SearchQuery = searchQuery
    };

    var timeline = await ruleSpecService.GetVersionTimelineAsync(gameId, filters, ct);
    return Results.Json(timeline);
})
.RequireAuthorization()
.WithName("GetVersionTimeline")
.WithTags("Versions");

// RULE-02: Get specific version
group.MapGet("/games/{gameId}/rulespec/versions/{version}", async (string gameId, string version, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
    var ruleSpec = await ruleSpecService.GetVersionAsync(gameId, version, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec version {Version} not found for game {GameId}", version, gameId);
        return Results.NotFound(new { error = "RuleSpec version not found" });
    }

    return Results.Json(ruleSpec);
});

// RULE-02: Compare two versions (diff)
group.MapGet("/games/{gameId}/rulespec/diff", async (string gameId, string? from, string? to, HttpContext context, RuleSpecService ruleSpecService, RuleSpecDiffService diffService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new { error = "Both 'from' and 'to' version parameters are required" });
    }

    logger.LogInformation("Computing diff between versions {FromVersion} and {ToVersion} for game {GameId}", from, to, gameId);

    var fromSpec = await ruleSpecService.GetVersionAsync(gameId, from, ct);
    var toSpec = await ruleSpecService.GetVersionAsync(gameId, to, ct);

    if (fromSpec == null || toSpec == null)
    {
        return Results.NotFound(new { error = "One or both RuleSpec versions not found" });
    }

    var diff = diffService.ComputeDiff(fromSpec, toSpec);
    return Results.Json(diff);
});

// EDIT-02: RuleSpec comment endpoints
group.MapPost("/games/{gameId}/rulespec/versions/{version}/comments", async (string gameId, string version, CreateRuleSpecCommentRequest request, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.CommentText))
    {
        return Results.BadRequest(new { error = "CommentText is required" });
    }

    try
    {
        logger.LogInformation("User {UserId} adding comment to RuleSpec {GameId} version {Version}", session.User.Id, gameId, version);
        var comment = await commentService.AddCommentAsync(gameId, version, request.AtomId, session.User.Id, request.CommentText, ct);
        logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
        return Results.Created($"/api/v1/games/{gameId}/rulespec/comments/{comment.Id}", comment);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to add comment: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

group.MapGet("/games/{gameId}/rulespec/versions/{version}/comments", async (string gameId, string version, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version}", session.User.Id, gameId, version);
    var response = await commentService.GetCommentsForVersionAsync(gameId, version, ct);
    return Results.Json(response);
});

group.MapPut("/games/{gameId}/rulespec/comments/{commentId:guid}", async (string gameId, Guid commentId, UpdateRuleSpecCommentRequest request, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.CommentText))
    {
        return Results.BadRequest(new { error = "CommentText is required" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating comment {CommentId}", session.User.Id, commentId);
        var comment = await commentService.UpdateCommentAsync(commentId, session.User.Id, request.CommentText, ct);
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
        logger.LogWarning("User {UserId} not authorized to update comment {CommentId}: {Error}", session.User.Id, commentId, ex.Message);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

group.MapDelete("/games/{gameId}/rulespec/comments/{commentId:guid}", async (string gameId, Guid commentId, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

    try
    {
        logger.LogInformation("User {UserId} deleting comment {CommentId}", session.User.Id, commentId);
        var deleted = await commentService.DeleteCommentAsync(commentId, session.User.Id, isAdmin, ct);

        if (!deleted)
        {
            return Results.NotFound(new { error = "Comment not found" });
        }

        logger.LogInformation("Comment {CommentId} deleted successfully", commentId);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException ex)
    {
        logger.LogWarning("User {UserId} not authorized to delete comment {CommentId}: {Error}", session.User.Id, commentId, ex.Message);
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} creating comment on RuleSpec {GameId} version {Version}", userId, gameId, version);
        var comment = await commentService.CreateCommentAsync(
            gameId,
            version,
            request.LineNumber,
            request.CommentText,
            userId);
        logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
        return Results.Created($"/api/v1/comments/{comment.Id}", comment);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (RuleSpecService)
        logger.LogError(ex, "Failed to create comment on RuleSpec {GameId} version {Version}", gameId, version);
        return Results.BadRequest(new { error = ex.Message });
    }
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} replying to comment {CommentId}", userId, commentId);
        var reply = await commentService.ReplyToCommentAsync(
            commentId,
            request.CommentText,
            userId);
        logger.LogInformation("Reply {ReplyId} created successfully", reply.Id);
        return Results.Created($"/api/v1/comments/{reply.Id}", reply);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for reply", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create reply to comment {CommentId}: {Error}", commentId, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (RuleSpecService)
        logger.LogError(ex, "Unexpected error creating reply to comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} (includeResolved: {IncludeResolved})",
        userId, gameId, version, includeResolved);

    var comments = await commentService.GetCommentsForRuleSpecAsync(gameId, version, includeResolved);
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} line {LineNumber}",
        userId, gameId, version, lineNumber);

    var comments = await commentService.GetCommentsForLineAsync(gameId, version, lineNumber);
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // SECURITY: Only Admin and Editor roles can resolve comments
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to resolve comment without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} resolving comment {CommentId} (resolveReplies: {ResolveReplies})",
            userId, commentId, resolveReplies);

        var comment = await commentService.ResolveCommentAsync(commentId, userId, resolveReplies);
        logger.LogInformation("Comment {CommentId} resolved successfully", commentId);
        return Results.Ok(comment);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for resolution", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (RuleSpecService)
        logger.LogError(ex, "Failed to resolve comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
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
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // SECURITY: Only Admin and Editor roles can unresolve comments
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to unresolve comment without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} unresolving comment {CommentId} (unresolveParent: {UnresolveParent})",
            userId, commentId, unresolveParent);

        var comment = await commentService.UnresolveCommentAsync(commentId, unresolveParent);
        logger.LogInformation("Comment {CommentId} unresolved successfully", commentId);
        return Results.Ok(comment);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for unresolve", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 400
        // Specific exception handling occurs in service layer (RuleSpecService)
        logger.LogError(ex, "Failed to unresolve comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
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

// 7. User search for mentions (autocomplete)
group.MapGet("/users/search", async (
    string query,
    MeepleAiDbContext db,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
    {
        return Results.Ok(Array.Empty<UserSearchResultDto>());
    }

    logger.LogInformation("User {UserId} searching for users with query: {Query}", userId, query);

    var users = await db.Users
        .Where(u => (u.DisplayName != null && u.DisplayName.Contains(query)) || u.Email.Contains(query))
        .OrderBy(u => u.DisplayName ?? u.Email)
        .Take(10)
        .AsNoTracking()
        .Select(u => new UserSearchResultDto(u.Id, u.DisplayName ?? u.Email, u.Email))
        .ToListAsync(ct);

    logger.LogInformation("Found {Count} users matching query: {Query}", users.Count, query);
    return Results.Ok(users);
})
.RequireAuthorization()
.WithName("SearchUsers")
.WithTags("Users")
.WithDescription("Search users by display name or email for @mention autocomplete (max 10 results)")
.Produces<IEnumerable<UserSearchResultDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

// EDIT-07: Bulk RuleSpec operations
group.MapPost("/rulespecs/bulk/export", async (BulkExportRequest request, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to export rule specs without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request?.RuleSpecIds == null || request.RuleSpecIds.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one rule spec ID must be provided" });
    }

    try
    {
        logger.LogInformation("User {UserId} exporting {Count} rule specs", session.User.Id, request.RuleSpecIds.Count);
        var zipBytes = await ruleSpecService.CreateZipArchiveAsync(request.RuleSpecIds, ct);

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
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (RuleSpecService)
        logger.LogError(ex, "Unexpected error during rule spec export");
        return Results.Problem("An error occurred during export", statusCode: StatusCodes.Status500InternalServerError);
    }
});

        return group;
    }
}
