using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles updating simple rule comments with ownership validation.
/// </summary>
public class UpdateSimpleRuleCommentCommandHandler : IRequestHandler<UpdateSimpleRuleCommentCommand, RuleSpecComment>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateSimpleRuleCommentCommandHandler> _logger;

    public UpdateSimpleRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<UpdateSimpleRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RuleSpecComment> Handle(UpdateSimpleRuleCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken);

        if (comment is null)
        {
            throw new InvalidOperationException($"Comment {command.CommentId} not found");
        }

        if (comment.User == null)
        {
            throw new InvalidOperationException("Comment user entity not loaded");
        }

        // Verify ownership
        if (comment.UserId != command.UserId)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} is not authorized to update this comment");
        }

        comment.CommentText = command.CommentText;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated simple comment {CommentId} by user {UserId}",
            command.CommentId, command.UserId);

        return new RuleSpecComment(
            comment.Id,
            comment.GameId.ToString(),
            comment.Version,
            comment.AtomId?.ToString(),
            comment.UserId.ToString(),
            comment.User.DisplayName ?? comment.User.Email,
            comment.CommentText,
            comment.CreatedAt,
            comment.UpdatedAt
        );
    }
}
