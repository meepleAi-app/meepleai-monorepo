using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of a specific version of a rule specification.
/// </summary>
internal class GetRuleSpecVersionQueryHandler : IQueryHandler<GetRuleSpecVersionQuery, RuleSpecDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetRuleSpecVersionQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RuleSpecDto?> Handle(GetRuleSpecVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var specEntity = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(r => r.Atoms)
            .Where(r => r.GameId == query.GameId && r.Version == query.Version)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (specEntity is null)
        {
            return null;
        }

        // Issue #2055: Include ETag for optimistic concurrency
        var etag = specEntity.RowVersion != null
            ? Convert.ToBase64String(specEntity.RowVersion)
            : null;

        return new RuleSpecDto(
            Id: specEntity.Id,
            GameId: specEntity.GameId,
            Version: specEntity.Version,
            CreatedAt: specEntity.CreatedAt,
            CreatedByUserId: specEntity.CreatedByUserId,
            ParentVersionId: specEntity.ParentVersionId,
            Atoms: specEntity.Atoms
                .OrderBy(a => a.SortOrder)
                .Select(a => new RuleAtomDto(
                    Id: a.Key,
                    Text: a.Text,
                    Section: a.Section,
                    Page: a.PageNumber?.ToString(CultureInfo.InvariantCulture),
                    Line: a.LineNumber?.ToString(CultureInfo.InvariantCulture)
                ))
                .ToList(),
            ETag: etag
        );
    }
}
