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
public partial class CreateRuleCommentCommandHandler : IRequestHandler<CreateRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateRuleCommentCommandHandler> _logger;

    private const int MaxCommentLength = 2000;

    // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
    [GeneratedRegex(@"@(\w{1,50})", RegexOptions.Compiled | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex MentionRegex();

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
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (!mentionedUsernames.Any())
            {
                return new List<string>();
            }

            // MA0011/MA0074: ToLower() required for EF Core SQL translation - ToLowerInvariant() not supported
            // EF Core translates ToLower() to SQL LOWER() which is deterministic and culture-safe in database context
#pragma warning disable MA0011, MA0074 // EF Core SQL translation limitation
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u =>
                    (u.DisplayName != null && mentionedUsernames.Contains(u.DisplayName.ToLower()))
                    || (u.Email != null && mentionedUsernames.Any(m => u.Email.ToLower().StartsWith(m))))
#pragma warning restore MA0011, MA0074
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

    private static void ValidateLineNumber(int? lineNumber)
    {
        if (lineNumber.HasValue && lineNumber.Value < 1)
        {
            throw new InvalidOperationException("Line number must be positive");
        }
    }
}
