using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetMyAiUsageDistributionsQuery.
/// Returns model, provider, and operation distribution breakdowns.
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal class GetMyAiUsageDistributionsQueryHandler
    : IQueryHandler<GetMyAiUsageDistributionsQuery, AiUsageDistributionsDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetMyAiUsageDistributionsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AiUsageDistributionsDto> Handle(
        GetMyAiUsageDistributionsQuery query, CancellationToken cancellationToken)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(query.Days - 1));

        var logs = await _dbContext.LlmCostLogs
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId && x.RequestDate >= cutoff)
            .Select(x => new { x.ModelId, x.Provider, x.Endpoint })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = logs.Count;
        if (total == 0)
            return new AiUsageDistributionsDto([], [], []);

        var models = logs
            .GroupBy(x => x.ModelId, StringComparer.Ordinal)
            .Select(g => new DistributionItemDto(g.Key, g.Count(), Math.Round(g.Count() * 100.0 / total, 1)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var providers = logs
            .GroupBy(x => x.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DistributionItemDto(g.Key, g.Count(), Math.Round(g.Count() * 100.0 / total, 1)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var operations = logs
            .GroupBy(x => x.Endpoint, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DistributionItemDto(g.Key, g.Count(), Math.Round(g.Count() * 100.0 / total, 1)))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new AiUsageDistributionsDto(models, providers, operations);
    }
}
