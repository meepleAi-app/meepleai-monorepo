using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles updating existing rule comments with ownership validation.
/// </summary>
internal class UpdateRuleCommentCommandHandler : IRequestHandler<UpdateRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateRuleCommentCommandHandler> _logger;

    private const int MaxCommentLength = 2000;

    public UpdateRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<UpdateRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleCommentDto> Handle(UpdateRuleCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommentText(command.CommentText);

        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException($"Comment {command.CommentId} not found");

        // Verify ownership
        if (comment.UserId != command.UserId)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} is not authorized to update this comment");
        }

        comment.CommentText = command.CommentText;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated comment {CommentId} by user {UserId}",
            command.CommentId, command.UserId);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(comment.Id, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to load updated comment");
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
