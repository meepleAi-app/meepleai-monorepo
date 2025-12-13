using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles replying to existing rule comments with thread depth validation.
/// </summary>
public partial class ReplyToRuleCommentCommandHandler : IRequestHandler<ReplyToRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReplyToRuleCommentCommandHandler> _logger;

    private const int MaxCommentLength = 2000;
    private const int MaxThreadDepth = 5;

    // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
    [GeneratedRegex(@"@(\w{1,50})", RegexOptions.Compiled | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex MentionRegex();

    public ReplyToRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ReplyToRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleCommentDto> Handle(ReplyToRuleCommentCommand command, CancellationToken cancellationToken)
    {
        ValidateCommentText(command.CommentText);

        // Load parent comment with validation
        var parentComment = await _dbContext.RuleSpecComments
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == command.ParentCommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException($"Parent comment {command.ParentCommentId} not found");

        // Validate thread depth (adding reply would increase depth by 1)
        var threadDepth = await CalculateThreadDepthAsync(command.ParentCommentId, cancellationToken).ConfigureAwait(false);
        if (threadDepth >= MaxThreadDepth - 1)
        {
            throw new InvalidOperationException(
                $"Maximum thread depth of {MaxThreadDepth} exceeded. Cannot reply to this comment.");
        }

        // Extract and resolve mentions
        var mentionedUserIdsStr = await ExtractMentionedUsersAsync(command.CommentText, cancellationToken).ConfigureAwait(false);
        var mentionedUserIds = mentionedUserIdsStr
            .Select(id => Guid.Parse(id))
            .ToList();

        // Create reply inheriting context from parent
        var reply = new RuleSpecCommentEntity
        {
            GameId = parentComment.GameId,
            Version = parentComment.Version,
            LineNumber = parentComment.LineNumber,
            ParentCommentId = command.ParentCommentId,
            CommentText = command.CommentText,
            UserId = command.UserId,
            MentionedUserIds = mentionedUserIds,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.RuleSpecComments.Add(reply);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created reply {ReplyId} by user {UserId} to parent {ParentId}",
            reply.Id, command.UserId, command.ParentCommentId);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(reply.Id, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to load created reply");
    }

    private async Task<int> CalculateThreadDepthAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var depth = 0;
        var currentId = commentId;
        var visited = new HashSet<Guid> { commentId }; // Circular reference protection

        while (depth < MaxThreadDepth + 1) // +1 to detect overflow
        {
            var parent = await _dbContext.RuleSpecComments
                .AsNoTracking()
                .Where(c => c.Id == currentId)
                .Select(c => c.ParentCommentId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (!parent.HasValue)
            {
                break;
            }

            if (visited.Contains(parent.Value))
            {
                _logger.LogWarning(
                    "Circular reference detected in comment thread: {CommentId} -> {ParentId}",
                    currentId, parent.Value);
                break;
            }

            depth++;
            currentId = parent.Value;
            visited.Add(currentId);
        }

        return depth;
    }

    private async Task<List<string>> ExtractMentionedUsersAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        try
        {
            var matches = MentionRegex().Matches(text);
            var mentionedUsernames = matches
                .Select(m => m.Groups[1].Value.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (!mentionedUsernames.Any())
            {
                return new List<string>();
            }

            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => (u.DisplayName != null && mentionedUsernames.Contains(u.DisplayName.ToLower(CultureInfo.InvariantCulture)))
                    || (u.Email != null && mentionedUsernames.Any(m => u.Email.ToLower(CultureInfo.InvariantCulture).StartsWith(m))))
                .Select(u => u.Id.ToString())
                .Distinct()
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (users.Count < mentionedUsernames.Count)
            {
                _logger.LogDebug(
                    "Resolved {ResolvedCount}/{TotalCount} mentions from text",
                    users.Count, mentionedUsernames.Count);
            }

            return users;
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Regex timeout while extracting mentions from text of length {Length}", text.Length);
            return new List<string>();
        }
    }

    private async Task<RuleCommentDto?> LoadCommentWithRelationsAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken).ConfigureAwait(false);

        return comment?.ToDto();
    }

    private static void ValidateCommentText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Comment text cannot be empty");
        }

        if (text.Length > MaxCommentLength)
        {
            throw new InvalidOperationException(
                $"Comment text exceeds maximum length of {MaxCommentLength} characters");
        }
    }
}
