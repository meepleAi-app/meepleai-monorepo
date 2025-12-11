using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of version history for a rule specification.
/// </summary>
public class GetVersionHistoryQueryHandler : IQueryHandler<GetVersionHistoryQuery, RuleSpecHistoryDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetVersionHistoryQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RuleSpecHistoryDto> Handle(GetVersionHistoryQuery query, CancellationToken cancellationToken)
    {
        var versions = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(r => r.Atoms)
            .Include(r => r.CreatedBy)
            .Where(r => r.GameId == query.GameId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var versionDtos = versions.Select(r => new RuleSpecVersionDto(
            r.Version,
            r.CreatedAt,
            r.Atoms.Count,
            r.CreatedBy != null ? r.CreatedBy.DisplayName ?? r.CreatedBy.Email : null
        )).ToList();

        return new RuleSpecHistoryDto(
            query.GameId,
            versionDtos,
            versionDtos.Count
        );
    }
}
