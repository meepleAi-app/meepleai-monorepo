using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles deletion of rule comments with ownership/admin authorization.
/// </summary>
internal class DeleteRuleCommentCommandHandler : IRequestHandler<DeleteRuleCommentCommand, bool>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<DeleteRuleCommentCommandHandler> _logger;

    public DeleteRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<DeleteRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(DeleteRuleCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException($"Comment {command.CommentId} not found");

        // Verify ownership or admin
        if (comment.UserId != command.UserId && !command.IsAdmin)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} is not authorized to delete this comment");
        }

        // Manually delete all replies (cascade delete - DeleteBehavior.Restrict requires manual handling)
        if (comment.Replies?.Count > 0)
        {
            await DeleteRepliesRecursivelyAsync(comment.Id, cancellationToken).ConfigureAwait(false);
        }

        _dbContext.RuleSpecComments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted comment {CommentId} by user {UserId} (admin: {IsAdmin})",
            command.CommentId, command.UserId, command.IsAdmin);

        return true;
    }

    private async Task DeleteRepliesRecursivelyAsync(Guid parentCommentId, CancellationToken cancellationToken)
    {
        var replies = await _dbContext.RuleSpecComments
            .Where(c => c.ParentCommentId == parentCommentId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var reply in replies)
        {
            // Recursively delete nested replies
            await DeleteRepliesRecursivelyAsync(reply.Id, cancellationToken).ConfigureAwait(false);
            _dbContext.RuleSpecComments.Remove(reply);
        }
    }
}
