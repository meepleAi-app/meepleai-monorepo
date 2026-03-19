using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Application.Validation;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles creation of rule comments with threading and @mention support.
/// </summary>
internal partial class CreateRuleCommentCommandHandler : IRequestHandler<CreateRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateRuleCommentCommandHandler> _logger;

    private const int MaxCommentLength = 2000;

    // FIX: Removed ExplicitCapture - it prevented Groups[1] capture which we need
    // MA0023 suppressed: we DO need the captured group
#pragma warning disable MA0023 // Add RegexOptions.ExplicitCapture
    [GeneratedRegex(@"@(\w{1,50})", RegexOptions.Compiled, matchTimeoutMilliseconds: 100)]
    private static partial Regex MentionRegex();
#pragma warning restore MA0023

    public CreateRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<CreateRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleCommentDto> Handle(CreateRuleCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommentText(command.CommentText);
        ValidateLineNumber(command.LineNumber);

        // Extract and resolve mentions
        var mentionedUserIdsStr = await ExtractMentionedUsersAsync(command.CommentText, cancellationToken).ConfigureAwait(false);
        var mentionedUserIds = mentionedUserIdsStr
            .Select(id => Guid.Parse(id))
            .ToList();

        var comment = new RuleSpecCommentEntity
        {
            GameId = GuidValidator.ParseRequired(command.GameId, nameof(command.GameId)),
            Version = command.Version,
            LineNumber = command.LineNumber,
            CommentText = command.CommentText,
            UserId = command.UserId,
            MentionedUserIds = mentionedUserIds,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created comment {CommentId} by user {UserId} for {GameId} v{Version} (line: {LineNumber})",
            comment.Id, command.UserId, command.GameId, command.Version, command.LineNumber);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(comment.Id, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to load created comment");
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
                .Where(m => !string.IsNullOrWhiteSpace(m))  // FIX: Filter empty mentions
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (mentionedUsernames.Count == 0)
            {
                return new List<string>();
            }

            // Load users to memory for case-insensitive comparison (client evaluation)
            // ToLowerInvariant() cannot be translated to SQL by EF Core
            // mentionedUsernames are already normalized to lowercase (line 85)
            var allUsers = await _dbContext.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.DisplayName, u.Email })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var users = allUsers
                .Where(u =>
                    (u.DisplayName != null && mentionedUsernames.Any(m => u.DisplayName.Equals(m, StringComparison.OrdinalIgnoreCase)))
                    || (u.Email != null && mentionedUsernames.Any(m => u.Email.StartsWith(m, StringComparison.OrdinalIgnoreCase))))
                .Select(u => new { u.Id, u.DisplayName, u.Email })
                .ToList();

            var userIds = users
                .Select(u => u.Id.ToString())
#pragma warning disable MA0002 // GUIDs converted to string are already distinct
                .Distinct()
#pragma warning restore MA0002
                .ToList();

            if (userIds.Count < mentionedUsernames.Count)
            {
                _logger.LogDebug(
                    "Resolved {ResolvedCount}/{TotalCount} mentions from text",
                    userIds.Count, mentionedUsernames.Count);
            }

            return userIds;
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

    private static void ValidateLineNumber(int? lineNumber)
    {
        if (lineNumber.HasValue && lineNumber.Value < 1)
        {
            throw new InvalidOperationException("Line number must be positive");
        }
    }
}
