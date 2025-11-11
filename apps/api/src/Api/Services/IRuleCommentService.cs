using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for managing rule specification comments with threading, mentions, and resolution.
/// Supports inline annotations, comment threading, and collaborative review workflows.
/// </summary>
public interface IRuleCommentService
{
    /// <summary>
    /// Create a top-level comment on a rule specification.
    /// Automatically extracts and resolves @mentions from comment text.
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="version">Rule specification version</param>
    /// <param name="lineNumber">Optional line number for inline annotations</param>
    /// <param name="commentText">Comment text content (may include @mentions)</param>
    /// <param name="userId">User ID creating the comment</param>
    /// <returns>Created comment DTO with all fields populated</returns>
    Task<RuleCommentDto> CreateCommentAsync(
        string gameId,
        string version,
        int? lineNumber,
        string commentText,
        Guid userId);

    /// <summary>
    /// Create a threaded reply to an existing comment.
    /// Inherits gameId, version, and lineNumber from parent comment.
    /// Automatically extracts and resolves @mentions from reply text.
    /// </summary>
    /// <param name="parentCommentId">Parent comment ID to reply to</param>
    /// <param name="commentText">Reply text content (may include @mentions)</param>
    /// <param name="userId">User ID creating the reply</param>
    /// <returns>Created reply DTO with ParentCommentId set</returns>
    /// <exception cref="NotFoundException">If parent comment not found</exception>
    /// <exception cref="ValidationException">If thread depth exceeds maximum limit</exception>
    Task<RuleCommentDto> ReplyToCommentAsync(
        Guid parentCommentId,
        string commentText,
        Guid userId);

    /// <summary>
    /// Get all comments for a specific rule specification version.
    /// Returns hierarchical structure with top-level comments and nested replies.
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="version">Rule specification version</param>
    /// <param name="includeResolved">If false, filters out resolved comments</param>
    /// <returns>List of top-level comments with nested replies</returns>
    Task<IReadOnlyList<RuleCommentDto>> GetCommentsForRuleSpecAsync(
        string gameId,
        string version,
        bool includeResolved = true);

    /// <summary>
    /// Get all comments for a specific line in a rule specification.
    /// Includes threaded replies for line-specific comments.
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="version">Rule specification version</param>
    /// <param name="lineNumber">Line number to retrieve comments for</param>
    /// <returns>List of comments for the specified line, ordered by creation date</returns>
    Task<IReadOnlyList<RuleCommentDto>> GetCommentsForLineAsync(
        string gameId,
        string version,
        int lineNumber);

    /// <summary>
    /// Mark a comment as resolved with resolution metadata.
    /// Optionally resolves all child replies recursively.
    /// </summary>
    /// <param name="commentId">Comment ID to resolve</param>
    /// <param name="resolvedByUserId">User ID performing the resolution</param>
    /// <param name="resolveReplies">If true, recursively resolves all child replies</param>
    /// <returns>Updated comment DTO with resolution fields set</returns>
    /// <exception cref="NotFoundException">If comment not found</exception>
    Task<RuleCommentDto> ResolveCommentAsync(
        Guid commentId,
        Guid resolvedByUserId,
        bool resolveReplies = false);

    /// <summary>
    /// Reopen a resolved comment, clearing resolution metadata.
    /// Optionally unresolves parent comment if this is a reply.
    /// </summary>
    /// <param name="commentId">Comment ID to unresolve</param>
    /// <param name="unresolveParent">If true and comment has parent, unresolves parent too</param>
    /// <returns>Updated comment DTO with resolution fields cleared</returns>
    /// <exception cref="NotFoundException">If comment not found</exception>
    Task<RuleCommentDto> UnresolveCommentAsync(
        Guid commentId,
        bool unresolveParent = false);

    /// <summary>
    /// Extract mentioned usernames from text and resolve to UserIds.
    /// Matches @username patterns and queries Users table for matches.
    /// </summary>
    /// <param name="text">Text containing potential @mentions</param>
    /// <returns>List of resolved UserIds (as strings) for mentioned users</returns>
    Task<List<string>> ExtractMentionedUsersAsync(string text);
}
