using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Validation;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles creation of simple rule comments (RuleSpecCommentService.AddCommentAsync pattern).
/// </summary>
public class CreateSimpleRuleCommentCommandHandler : IRequestHandler<CreateSimpleRuleCommentCommand, RuleSpecComment>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateSimpleRuleCommentCommandHandler> _logger;

    public CreateSimpleRuleCommentCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<CreateSimpleRuleCommentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RuleSpecComment> Handle(CreateSimpleRuleCommentCommand command, CancellationToken cancellationToken)
    {
        // Validate version exists
        var versionExists = await _dbContext.RuleSpecs
            .AnyAsync(r => r.GameId.ToString() == command.GameId && r.Version == command.Version, cancellationToken);

        if (!versionExists)
        {
            throw new InvalidOperationException($"RuleSpec version {command.Version} not found for game {command.GameId}");
        }

        // Get user info for response
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User {command.UserId} not found");
        }

        var comment = new RuleSpecCommentEntity
        {
            GameId = GuidValidator.ParseRequired(command.GameId, nameof(command.GameId)),
            Version = command.Version,
            AtomId = GuidValidator.ParseOptional(command.AtomId, nameof(command.AtomId)),
            UserId = command.UserId,
            CommentText = command.CommentText,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created simple comment {CommentId} by user {UserId} for {GameId} v{Version} (atom: {AtomId})",
            comment.Id, command.UserId, command.GameId, command.Version, command.AtomId);

        return new RuleSpecComment(
            comment.Id,
            comment.GameId.ToString(),
            comment.Version,
            comment.AtomId?.ToString(),
            comment.UserId.ToString(),
            user.DisplayName ?? user.Email,
            comment.CommentText,
            comment.CreatedAt,
            comment.UpdatedAt
        );
    }
}
