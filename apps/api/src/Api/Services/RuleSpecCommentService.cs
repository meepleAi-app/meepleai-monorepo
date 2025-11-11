using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class RuleSpecCommentService
{
    private readonly MeepleAiDbContext _dbContext;

    public RuleSpecCommentService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RuleSpecComment> AddCommentAsync(
        string gameId,
        string version,
        string? atomId,
        string userId,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        // Validate version exists
        var versionExists = await _dbContext.RuleSpecs
            .AnyAsync(r => r.GameId.ToString() == gameId && r.Version == version, cancellationToken);

        if (!versionExists)
        {
            throw new InvalidOperationException($"RuleSpec version {version} not found for game {gameId}");
        }

        // Get user info for response
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        var comment = new RuleSpecCommentEntity
        {
            GameId = Guid.Parse(gameId),
            Version = version,
            AtomId = string.IsNullOrEmpty(atomId) ? null : Guid.Parse(atomId),
            UserId = Guid.Parse(userId),
            CommentText = commentText,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.RuleSpecComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

    public async Task<RuleSpecCommentsResponse> GetCommentsForVersionAsync(
        string gameId,
        string version,
        CancellationToken cancellationToken = default)
    {
        var gameGuid = Guid.Parse(gameId);
        var comments = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Where(c => c.GameId == gameGuid && c.Version == version)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new RuleSpecComment(
                c.Id,
                c.GameId.ToString(),
                c.Version,
                c.AtomId != null ? c.AtomId.ToString() : null,
                c.UserId.ToString(),
                c.User != null ? (c.User.DisplayName ?? c.User.Email) : "Unknown",
                c.CommentText,
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return new RuleSpecCommentsResponse(gameId, version, comments, comments.Count);
    }

    public async Task<RuleSpecComment> UpdateCommentAsync(
        Guid commentId,
        string userId,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        if (comment is null)
        {
            throw new InvalidOperationException($"Comment {commentId} not found");
        }

        if (comment.User == null)
        {
            throw new InvalidOperationException("Comment user entity not loaded");
        }

        // Verify ownership
        var userGuid = Guid.Parse(userId);
        if (comment.UserId != userGuid)
        {
            throw new UnauthorizedAccessException($"User {userId} is not authorized to update this comment");
        }

        comment.CommentText = commentText;
        comment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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

    public async Task<bool> DeleteCommentAsync(
        Guid commentId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.RuleSpecComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        if (comment is null)
        {
            return false;
        }

        // Verify ownership or admin
        var userGuid = Guid.Parse(userId);
        if (comment.UserId != userGuid && !isAdmin)
        {
            throw new UnauthorizedAccessException($"User {userId} is not authorized to delete this comment");
        }

        _dbContext.RuleSpecComments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
