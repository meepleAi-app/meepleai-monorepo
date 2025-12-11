using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of rule comments for a specific game version.
/// </summary>
public class GetRuleCommentsQueryHandler : IRequestHandler<GetRuleCommentsQuery, IReadOnlyList<RuleCommentDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRuleCommentsQueryHandler> _logger;

    public GetRuleCommentsQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetRuleCommentsQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RuleCommentDto>> Handle(GetRuleCommentsQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.Replies) // Nested replies (second level)
                    .ThenInclude(rr => rr.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.ResolvedByUser)
            .Include(c => c.Replies)
                .ThenInclude(r => r.Replies) // Nested replies (second level)
                    .ThenInclude(rr => rr.ResolvedByUser)
            .Include(c => c.ResolvedByUser)
            .AsNoTracking()
            .Where(c => c.GameId.ToString() == query.GameId && c.Version == query.Version && c.ParentCommentId == null);

        if (!query.IncludeResolved)
        {
            dbQuery = dbQuery.Where(c => !c.IsResolved);
        }

        var comments = await dbQuery
            .OrderBy(c => c.CreatedAt)
            .Take(100) // Limit results to prevent memory issues
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Retrieved {CommentCount} comments for {GameId} v{Version} (includeResolved: {IncludeResolved})",
            comments.Count, query.GameId, query.Version, query.IncludeResolved);

        return comments.Select(c => c.ToDto()).ToList();
    }
}
