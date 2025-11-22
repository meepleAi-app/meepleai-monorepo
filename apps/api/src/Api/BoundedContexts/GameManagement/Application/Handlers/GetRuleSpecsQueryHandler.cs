using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get rule specifications query for a game.
/// Note: RuleSpec is currently in infrastructure layer.
/// This may be moved to a dedicated bounded context (GameManagement or KnowledgeBase) in the future.
/// </summary>
public class GetRuleSpecsQueryHandler : IQueryHandler<GetRuleSpecsQuery, IReadOnlyList<RuleSpecDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetRuleSpecsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<RuleSpecDto>> Handle(GetRuleSpecsQuery query, CancellationToken cancellationToken)
    {
        var ruleSpecs = await _dbContext.RuleSpecs
            .Where(rs => rs.GameId == query.GameId)
            .Include(rs => rs.Atoms)
            .OrderByDescending(rs => rs.CreatedAt)
            .ToListAsync(cancellationToken);

        return ruleSpecs.Select(rs => new RuleSpecDto(
            Id: rs.Id,
            GameId: rs.GameId,
            Version: rs.Version,
            CreatedAt: rs.CreatedAt,
            CreatedByUserId: rs.CreatedByUserId,
            ParentVersionId: rs.ParentVersionId,
            Atoms: rs.Atoms
                .OrderBy(a => a.SortOrder)
                .Select(a => new RuleAtomDto(
                    Id: a.Key,
                    Text: a.Text,
                    Section: a.Section,
                    Page: a.PageNumber?.ToString(),
                    Line: a.LineNumber?.ToString()
                ))
                .ToList()
        )).ToList();
    }
}
