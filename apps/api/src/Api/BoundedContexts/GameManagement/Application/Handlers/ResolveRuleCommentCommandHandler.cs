using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles resolving rule comments with optional recursive reply resolution.
/// </summary>
public class ResolveRuleCommentCommandHandler : IRequestHandler<ResolveRuleCommentCommand, RuleCommentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResolveRuleCommentCommandHandler> _logger;

    public ResolveRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ResolveRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleCommentDto> Handle(ResolveRuleCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == command.CommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException($"Comment {command.CommentId} not found");

        // Authorization: Only comment owner or admin can resolve
        if (comment.UserId != command.ResolvedByUserId && !command.IsAdmin)
        {
            throw new UnauthorizedAccessException(
                $"User {command.ResolvedByUserId} is not authorized to resolve this comment");
        }

        comment.IsResolved = true;
        comment.ResolvedByUserId = command.ResolvedByUserId;
        comment.ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Recursively resolve replies if requested
        if (command.ResolveReplies && comment.Replies.Any())
        {
            await ResolveRepliesRecursiveAsync(comment.Id, command.ResolvedByUserId, cancellationToken).ConfigureAwait(false);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Resolved comment {CommentId} by user {ResolvedBy} (includeReplies: {ResolveReplies}, admin: {IsAdmin})",
            command.CommentId, command.ResolvedByUserId, command.ResolveReplies, command.IsAdmin);

        // Reload with navigation properties
        return await LoadCommentWithRelationsAsync(command.CommentId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to load resolved comment");
    }

    private async Task ResolveRepliesRecursiveAsync(
        Guid parentCommentId,
        Guid resolvedByUserId,
        CancellationToken cancellationToken)
    {
        // Load all descendants in a single query to avoid N+1
        var allDescendants = await LoadAllDescendantsAsync(parentCommentId, cancellationToken).ConfigureAwait(false);

        var resolvedAt = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var descendant in allDescendants)
        {
            descendant.IsResolved = true;
            descendant.ResolvedByUserId = resolvedByUserId;
            descendant.ResolvedAt = resolvedAt;
            descendant.UpdatedAt = resolvedAt;
        }

        _logger.LogDebug(
            "Resolved {Count} descendant comments for parent {ParentId}",
            allDescendants.Count, parentCommentId);
    }

    private async Task<List<RuleSpecCommentEntity>> LoadAllDescendantsAsync(
        Guid parentCommentId,
        CancellationToken cancellationToken)
    {
        var descendants = new List<RuleSpecCommentEntity>();
        var currentLevel = new List<Guid> { parentCommentId };
        var visited = new HashSet<Guid> { parentCommentId }; // Circular reference protection
        var maxDepth = 10; // Safety limit
        var depth = 0;

        while (currentLevel.Any() && depth < maxDepth)
        {
            var children = await _dbContext.RuleSpecComments
                .Where(c => currentLevel.Contains(c.ParentCommentId!.Value))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (!children.Any())
            {
                break;
            }

            // Check for circular references
            var newIds = children.Select(c => c.Id).Where(id => !visited.Contains(id)).ToList();
            if (!newIds.Any() && children.Any())
            {
                _logger.LogWarning(
                    "Circular reference detected while loading descendants of {ParentId} at depth {Depth}",
                    parentCommentId, depth);
                break;
            }

            descendants.AddRange(children);
            currentLevel = newIds;
            visited.UnionWith(newIds);
            depth++;
        }

        return descendants;
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
}
