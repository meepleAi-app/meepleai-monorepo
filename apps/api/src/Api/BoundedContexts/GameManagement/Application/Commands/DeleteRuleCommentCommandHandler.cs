using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles deletion of rule comments with ownership/admin authorization.
/// </summary>
internal class DeleteRuleCommentCommandHandler : IRequestHandler<DeleteRuleCommentCommand, RuleCommentDeletion>
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

    public async Task<RuleCommentDeletion> Handle(DeleteRuleCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Issue #3866: `.AsTracking()` is REQUIRED on both reads of this handler. The DbContext
        // default is NoTracking (PERF-06), so the comment and its replies came back as SEPARATE
        // detached instances of the same rows: Remove() had to attach each of them, and the manual
        // cascade in DeleteRepliesRecursivelyAsync collided with the graph reachable from the root.
        // With tracking there is one instance per row, which is what a delete needs.
        var comment = await _dbContext.RuleSpecComments
            .AsTracking()
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new NotFoundException("RuleComment", command.CommentId.ToString());

        // Verify ownership or admin
        if (comment.UserId != command.UserId && !command.IsAdmin)
        {
            throw new AccessDeniedException($"User {command.UserId} is not authorized to delete this comment");
        }

        // Manually delete all replies (cascade delete - DeleteBehavior.Restrict requires manual handling)
        var repliesDeleted = 0;
        if (comment.Replies?.Count > 0)
        {
            repliesDeleted = await DeleteRepliesRecursivelyAsync(comment.Id, cancellationToken).ConfigureAwait(false);
        }

        _dbContext.RuleSpecComments.Remove(comment);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Issue #5048. The tracker is cleared on every failed save: it still holds the Deleted
            // entries, and the audit behaviour writes its Error row with a SaveChanges on this same
            // scoped context — which would resend those DELETEs, fail again, and lose the audit row
            // in its catch. Besides the race below, a reply inserted under the root between our read
            // and our write fails the root DELETE on the foreign key, and needs the same clean-up.
            _dbContext.ChangeTracker.Clear();

            // A DELETE that touched 0 rows means another request removed the row between our read
            // and our write — e.g. a double click, two tabs, or an admin and the author at once. If
            // the root comment is the one that is gone, this is the same request as "delete an
            // absent comment", seen a few milliseconds later, and it gets the same answer (404)
            // instead of the 409 "reload and retry" the middleware gives a concurrency exception. If
            // the root is still there, a REPLY vanished under the cascade: that one really is a
            // conflict, and a retry would succeed, so it stays a 409.
            if (ex is DbUpdateConcurrencyException
                && !await _dbContext.RuleSpecComments
                    .AnyAsync(c => c.Id == command.CommentId, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new NotFoundException("RuleComment", command.CommentId.ToString(), ex);
            }

            throw;
        }

        _logger.LogInformation(
            "Deleted comment {CommentId} by user {UserId} (admin: {IsAdmin}), {RepliesDeleted} replies with it",
            command.CommentId, command.UserId, command.IsAdmin, repliesDeleted);

        return new RuleCommentDeletion(comment.UserId, repliesDeleted);
    }

    /// <returns>The replies removed under <paramref name="parentCommentId"/>, at every depth.</returns>
    private async Task<int> DeleteRepliesRecursivelyAsync(Guid parentCommentId, CancellationToken cancellationToken)
    {
        var replies = await _dbContext.RuleSpecComments
            .AsTracking()
            .Where(c => c.ParentCommentId == parentCommentId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var removed = 0;
        foreach (var reply in replies)
        {
            // Recursively delete nested replies
            removed += await DeleteRepliesRecursivelyAsync(reply.Id, cancellationToken).ConfigureAwait(false);
            _dbContext.RuleSpecComments.Remove(reply);
            removed++;
        }

        return removed;
    }
}
