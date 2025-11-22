using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of rule comments for a specific line number.
/// </summary>
public class GetCommentsForLineQueryHandler : IRequestHandler<GetCommentsForLineQuery, IReadOnlyList<RuleCommentDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetCommentsForLineQueryHandler> _logger;

    public GetCommentsForLineQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetCommentsForLineQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RuleCommentDto>> Handle(GetCommentsForLineQuery query, CancellationToken cancellationToken)
    {
        if (query.LineNumber < 1)
        {
            throw new InvalidOperationException("Line number must be positive");
        }

        var comments = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTracking()
            .Where(c => c.GameId.ToString() == query.GameId
                && c.Version == query.Version
                && c.LineNumber == query.LineNumber
                && c.ParentCommentId == null) // Only top-level comments for this line
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {CommentCount} comments for {GameId} v{Version} line {LineNumber}",
            comments.Count, query.GameId, query.Version, query.LineNumber);

        return comments.Select(MapToDto).ToList();
    }

    private static RuleCommentDto MapToDto(RuleSpecCommentEntity entity)
    {
        return new RuleCommentDto(
            Id: entity.Id,
            GameId: entity.GameId.ToString(),
            Version: entity.Version,
            LineNumber: entity.LineNumber,
            LineContext: entity.LineContext,
            ParentCommentId: entity.ParentCommentId,
            CommentText: entity.CommentText,
            UserId: entity.UserId.ToString(),
            UserDisplayName: entity.User?.DisplayName ?? "Unknown",
            IsResolved: entity.IsResolved,
            ResolvedByUserId: entity.ResolvedByUserId?.ToString(),
            ResolvedByDisplayName: entity.ResolvedByUser?.DisplayName,
            ResolvedAt: entity.ResolvedAt,
            MentionedUserIds: entity.MentionedUserIds.Select(id => id.ToString()).ToList(),
            Replies: entity.Replies?.Select(MapToDto).ToList() ?? new List<RuleCommentDto>(),
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }
}
