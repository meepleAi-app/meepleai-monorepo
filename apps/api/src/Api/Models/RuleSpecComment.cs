namespace Api.Models;

/// <summary>
/// Represents a comment on a specific RuleSpec version
/// </summary>
public record RuleSpecComment(
    Guid Id,
    string GameId,
    string Version,
    string? AtomId,
    string UserId,
    string UserDisplayName,
    string CommentText,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Request to create a new comment on a RuleSpec version
/// </summary>
public record CreateRuleSpecCommentRequest(
    string? AtomId,
    string CommentText
);

/// <summary>
/// Request to update an existing comment
/// </summary>
public record UpdateRuleSpecCommentRequest(
    string CommentText
);

/// <summary>
/// Response containing a list of comments for a RuleSpec version
/// </summary>
public record RuleSpecCommentsResponse(
    string GameId,
    string Version,
    IReadOnlyList<RuleSpecComment> Comments,
    int TotalComments
);
