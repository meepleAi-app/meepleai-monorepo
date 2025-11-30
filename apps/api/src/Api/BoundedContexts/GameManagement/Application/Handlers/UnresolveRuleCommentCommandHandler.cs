using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles unresolving rule comments with optional parent unresolve.
/// </summary>
public class UnresolveRuleCommentCommandHandler : IRequestHandler<UnresolveRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnresolveRuleCommentCommandHandler> _logger;

    public UnresolveRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<UnresolveRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleCommentDto> Handle(UnresolveRuleCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
            ?? throw new InvalidOperationException($"Comment {command.CommentId} not found");

        // Authorization: Only comment owner or admin can unresolve
        if (comment.UserId != command.UserId && !command.IsAdmin)
        {
            throw new UnauthorizedAccessException(
                $"User {command.UserId} is not authorized to unresolve this comment");
        }

        comment.IsResolved = false;
        comment.ResolvedByUserId = null;
        comment.ResolvedAt = null;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Unresolve parent if requested and exists
        if (command.UnresolveParent && comment.ParentCommentId.HasValue)
        {
            var parent = await _dbContext.RuleSpecComments
                .FirstOrDefaultAsync(c => c.Id == comment.ParentCommentId.Value, cancellationToken);

            if (parent != null && parent.IsResolved)
            {
                parent.IsResolved = false;
                parent.ResolvedByUserId = null;
                parent.ResolvedAt = null;
                parent.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                _logger.LogInformation(
                    "Unresolved parent comment {ParentId} due to child {ChildId} unresolve",
                    parent.Id, command.CommentId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Unresolved comment {CommentId} by user {UserId} (unresolveParent: {UnresolveParent}, admin: {IsAdmin})",
            command.CommentId, command.UserId, command.UnresolveParent, command.IsAdmin);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(command.CommentId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to load unresolved comment");
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
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        return comment?.ToDto();
    }
}
