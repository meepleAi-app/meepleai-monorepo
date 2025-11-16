using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles deletion of simple rule comments with ownership/admin authorization.
/// </summary>
public class DeleteSimpleRuleCommentCommandHandler : IRequestHandler<DeleteSimpleRuleCommentCommand, bool>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<DeleteSimpleRuleCommentCommandHandler> _logger;

    public DeleteSimpleRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<DeleteSimpleRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteSimpleRuleCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.RuleSpecComments
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
            ?? throw new InvalidOperationException($"Comment {command.CommentId} not found");

        // Verify ownership or admin
        if (comment.UserId != command.UserId && !command.IsAdmin)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} is not authorized to delete this comment");
        }

        _dbContext.RuleSpecComments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted simple comment {CommentId} by user {UserId} (admin: {IsAdmin})",
            command.CommentId, command.UserId, command.IsAdmin);

        return true;
    }
}
