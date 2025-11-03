using System.Text.RegularExpressions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service implementation for rule specification comments with threading, mentions, and resolution.
/// Provides collaborative review capabilities with inline annotations and @mention notifications.
/// </summary>
public partial class RuleCommentService : IRuleCommentService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<RuleCommentService> _logger;
    private readonly TimeProvider _timeProvider;

    private const int MaxCommentLength = 10000;
    private const int MaxThreadDepth = 5;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    // Compiled regex for performance: matches @username patterns
    [GeneratedRegex(@"@(\w+)", RegexOptions.Compiled, matchTimeoutMilliseconds: 100)]
    private static partial Regex MentionRegex();

    public RuleCommentService(
        MeepleAiDbContext dbContext,
        ILogger<RuleCommentService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RuleCommentDto> CreateCommentAsync(
        string gameId,
        string version,
        int? lineNumber,
        string commentText,
        string userId)
    {
        ValidateCommentText(commentText);
        ValidateLineNumber(lineNumber);

        // Extract and resolve mentions
        var mentionedUserIds = await ExtractMentionedUsersAsync(commentText);

        var comment = new RuleSpecCommentEntity
        {
            GameId = gameId,
            Version = version,
            LineNumber = lineNumber,
            CommentText = commentText,
            UserId = userId,
            MentionedUserIds = mentionedUserIds,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created comment {CommentId} by user {UserId} for {GameId} v{Version} (line: {LineNumber})",
            comment.Id, userId, gameId, version, lineNumber);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(comment.Id)
            ?? throw new InvalidOperationException("Failed to load created comment");
    }

    public async Task<RuleCommentDto> ReplyToCommentAsync(
        Guid parentCommentId,
        string commentText,
        string userId)
    {
        ValidateCommentText(commentText);

        // Load parent comment with validation
        var parentComment = await _dbContext.RuleSpecComments
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == parentCommentId)
            ?? throw new NotFoundException($"Parent comment {parentCommentId} not found");

        // Validate thread depth
        var threadDepth = await CalculateThreadDepthAsync(parentCommentId);
        if (threadDepth >= MaxThreadDepth)
        {
            throw new ValidationException(
                $"Maximum thread depth of {MaxThreadDepth} exceeded. Cannot reply to this comment.");
        }

        // Extract and resolve mentions
        var mentionedUserIds = await ExtractMentionedUsersAsync(commentText);

        // Create reply inheriting context from parent
        var reply = new RuleSpecCommentEntity
        {
            GameId = parentComment.GameId,
            Version = parentComment.Version,
            LineNumber = parentComment.LineNumber,
            ParentCommentId = parentCommentId,
            CommentText = commentText,
            UserId = userId,
            MentionedUserIds = mentionedUserIds,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.RuleSpecComments.Add(reply);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created reply {ReplyId} by user {UserId} to parent {ParentId}",
            reply.Id, userId, parentCommentId);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(reply.Id)
            ?? throw new InvalidOperationException("Failed to load created reply");
    }

    public async Task<IReadOnlyList<RuleCommentDto>> GetCommentsForRuleSpecAsync(
        string gameId,
        string version,
        bool includeResolved = true)
    {
        var query = _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.Replies) // Nested replies (second level)
                    .ThenInclude(rr => rr.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.Replies)
                .ThenInclude(r => r.Replies) // Nested replies (second level)
                    .ThenInclude(rr => rr.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTracking()
            .Where(c => c.GameId == gameId && c.Version == version && c.ParentCommentId == null);

        if (!includeResolved)
        {
            query = query.Where(c => !c.IsResolved);
        }

        var comments = await query
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<RuleCommentDto>> GetCommentsForLineAsync(
        string gameId,
        string version,
        int lineNumber)
    {
        ValidateLineNumber(lineNumber);

        var comments = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTracking()
            .Where(c => c.GameId == gameId
                && c.Version == version
                && c.LineNumber == lineNumber
                && c.ParentCommentId == null) // Only top-level comments for this line
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(MapToDto).ToList();
    }

    public async Task<RuleCommentDto> ResolveCommentAsync(
        Guid commentId,
        string resolvedByUserId,
        bool resolveReplies = false)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == commentId)
            ?? throw new NotFoundException($"Comment {commentId} not found");

        comment.IsResolved = true;
        comment.ResolvedByUserId = resolvedByUserId;
        comment.ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Recursively resolve replies if requested
        if (resolveReplies && comment.Replies.Any())
        {
            await ResolveRepliesRecursiveAsync(comment.Replies, resolvedByUserId);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Resolved comment {CommentId} by user {ResolvedBy} (includeReplies: {ResolveReplies})",
            commentId, resolvedByUserId, resolveReplies);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(commentId)
            ?? throw new InvalidOperationException("Failed to load resolved comment");
    }

    public async Task<RuleCommentDto> UnresolveCommentAsync(
        Guid commentId,
        bool unresolveParent = false)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == commentId)
            ?? throw new NotFoundException($"Comment {commentId} not found");

        comment.IsResolved = false;
        comment.ResolvedByUserId = null;
        comment.ResolvedAt = null;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Unresolve parent if requested and exists
        if (unresolveParent && comment.ParentCommentId.HasValue)
        {
            var parent = await _dbContext.RuleSpecComments
                .FirstOrDefaultAsync(c => c.Id == comment.ParentCommentId.Value);

            if (parent != null && parent.IsResolved)
            {
                parent.IsResolved = false;
                parent.ResolvedByUserId = null;
                parent.ResolvedAt = null;
                parent.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                _logger.LogInformation(
                    "Unresolved parent comment {ParentId} due to child {ChildId} unresolve",
                    parent.Id, commentId);
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Unresolved comment {CommentId} (unresolveParent: {UnresolveParent})",
            commentId, unresolveParent);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(commentId)
            ?? throw new InvalidOperationException("Failed to load unresolved comment");
    }

    public async Task<List<string>> ExtractMentionedUsersAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        try
        {
            // Extract unique usernames from @mentions
            var matches = MentionRegex().Matches(text);
            var mentionedUsernames = matches
                .Select(m => m.Groups[1].Value.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (!mentionedUsernames.Any())
            {
                return new List<string>();
            }

            // Query users: exact DisplayName match (case-insensitive) or Email prefix match
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => (u.DisplayName != null && mentionedUsernames.Contains(u.DisplayName.ToLower()))
                    || mentionedUsernames.Any(m => u.Email.ToLower().StartsWith(m)))
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync();

            // Log if some mentions couldn't be resolved
            if (users.Count < mentionedUsernames.Count)
            {
                var resolvedCount = users.Count;
                var totalMentions = mentionedUsernames.Count;
                _logger.LogDebug(
                    "Resolved {ResolvedCount}/{TotalCount} mentions from text",
                    resolvedCount, totalMentions);
            }

            return users;
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout while extracting mentions from text of length {Length}", text.Length);
            return new List<string>();
        }
    }

    // Private helper methods

    private async Task<RuleCommentDto?> LoadCommentWithRelationsAsync(Guid commentId)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(c => c.Id == commentId);

        return comment != null ? MapToDto(comment) : null;
    }

    private async Task<int> CalculateThreadDepthAsync(Guid commentId)
    {
        var depth = 0;
        var currentId = commentId;

        while (depth < MaxThreadDepth + 1) // +1 to detect overflow
        {
            var parent = await _dbContext.RuleSpecComments
                .AsNoTracking()
                .Where(c => c.Id == currentId)
                .Select(c => c.ParentCommentId)
                .FirstOrDefaultAsync();

            if (!parent.HasValue)
            {
                break;
            }

            depth++;
            currentId = parent.Value;
        }

        return depth;
    }

    private async Task ResolveRepliesRecursiveAsync(IEnumerable<RuleSpecCommentEntity> replies, string resolvedByUserId)
    {
        foreach (var reply in replies)
        {
            reply.IsResolved = true;
            reply.ResolvedByUserId = resolvedByUserId;
            reply.ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime;
            reply.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            // Load child replies and recurse
            var childReplies = await _dbContext.RuleSpecComments
                .Where(c => c.ParentCommentId == reply.Id)
                .ToListAsync();

            if (childReplies.Any())
            {
                await ResolveRepliesRecursiveAsync(childReplies, resolvedByUserId);
            }
        }
    }

    private static RuleCommentDto MapToDto(RuleSpecCommentEntity entity)
    {
        return new RuleCommentDto(
            Id: entity.Id,
            GameId: entity.GameId,
            Version: entity.Version,
            LineNumber: entity.LineNumber,
            LineContext: entity.LineContext,
            ParentCommentId: entity.ParentCommentId,
            CommentText: entity.CommentText,
            UserId: entity.UserId,
            UserDisplayName: entity.User?.DisplayName ?? "Unknown",
            IsResolved: entity.IsResolved,
            ResolvedByUserId: entity.ResolvedByUserId,
            ResolvedByDisplayName: entity.ResolvedByUser?.DisplayName,
            ResolvedAt: entity.ResolvedAt,
            MentionedUserIds: entity.MentionedUserIds,
            Replies: entity.Replies?.Select(MapToDto).ToList() ?? new List<RuleCommentDto>(),
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }

    private static void ValidateCommentText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Comment text cannot be empty");
        }

        if (text.Length > MaxCommentLength)
        {
            throw new ValidationException(
                $"Comment text exceeds maximum length of {MaxCommentLength} characters");
        }
    }

    private static void ValidateLineNumber(int? lineNumber)
    {
        if (lineNumber.HasValue && lineNumber.Value < 1)
        {
            throw new ValidationException("Line number must be positive");
        }
    }
}

// Exception types (should exist in Api.Models or create if missing)
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
