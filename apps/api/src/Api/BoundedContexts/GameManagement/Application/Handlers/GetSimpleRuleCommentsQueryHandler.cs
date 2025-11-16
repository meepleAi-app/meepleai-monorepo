using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Validation;
using Api.Infrastructure;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of simple rule comments for a specific version.
/// </summary>
public class GetSimpleRuleCommentsQueryHandler : IRequestHandler<GetSimpleRuleCommentsQuery, RuleSpecCommentsResponse>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetSimpleRuleCommentsQueryHandler> _logger;

    public GetSimpleRuleCommentsQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetSimpleRuleCommentsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RuleSpecCommentsResponse> Handle(GetSimpleRuleCommentsQuery query, CancellationToken cancellationToken)
    {
        var gameGuid = GuidValidator.ParseRequired(query.GameId, nameof(query.GameId));
        var comments = await _dbContext.RuleSpecComments
            .Include(c => c.User)
            .Where(c => c.GameId == gameGuid && c.Version == query.Version)
            .OrderBy(c => c.CreatedAt)
            .Take(100) // Limit results to prevent memory issues
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

        _logger.LogDebug(
            "Retrieved {CommentCount} simple comments for {GameId} v{Version}",
            comments.Count, query.GameId, query.Version);

        return new RuleSpecCommentsResponse(query.GameId, query.Version, comments, comments.Count);
    }
}
