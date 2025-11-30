using System.Globalization;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles computing differences between RuleSpec versions using domain service.
/// </summary>
public class ComputeRuleSpecDiffQueryHandler : IRequestHandler<ComputeRuleSpecDiffQuery, RuleSpecDiff>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecDiffDomainService _diffService;
    private readonly ILogger<ComputeRuleSpecDiffQueryHandler> _logger;

    public ComputeRuleSpecDiffQueryHandler(
        MeepleAiDbContext dbContext,
        RuleSpecDiffDomainService diffService,
        ILogger<ComputeRuleSpecDiffQueryHandler> logger)
    {
        _dbContext = dbContext;
        _diffService = diffService;
        _logger = logger;
    }

    public async Task<RuleSpecDiff> Handle(ComputeRuleSpecDiffQuery query, CancellationToken cancellationToken)
    {
        // Load FROM version
        var fromEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.GameId == query.GameId && r.Version == query.FromVersion, cancellationToken)
            ?? throw new InvalidOperationException($"RuleSpec version {query.FromVersion} not found for game {query.GameId}");

        var fromRuleSpec = new RuleSpec(
            gameId: fromEntity.GameId.ToString(),
            version: fromEntity.Version,
            rules: fromEntity.Atoms.Select(r => new RuleAtom(
                id: r.Id.ToString(),
                text: r.Text,
                section: r.Section,
                page: r.PageNumber?.ToString(CultureInfo.InvariantCulture),
                line: r.LineNumber?.ToString(CultureInfo.InvariantCulture)
            )).ToList(),
            createdAt: fromEntity.CreatedAt
        );

        // Load TO version
        var toEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.GameId == query.GameId && r.Version == query.ToVersion, cancellationToken)
            ?? throw new InvalidOperationException($"RuleSpec version {query.ToVersion} not found for game {query.GameId}");

        var toRuleSpec = new RuleSpec(
            gameId: toEntity.GameId.ToString(),
            version: toEntity.Version,
            rules: toEntity.Atoms.Select(r => new RuleAtom(
                id: r.Id.ToString(),
                text: r.Text,
                section: r.Section,
                page: r.PageNumber?.ToString(CultureInfo.InvariantCulture),
                line: r.LineNumber?.ToString(CultureInfo.InvariantCulture)
            )).ToList(),
            createdAt: toEntity.CreatedAt
        );

        // Compute diff using domain service
        var diff = _diffService.ComputeDiff(fromRuleSpec, toRuleSpec);

        _logger.LogInformation(
            "Computed diff for {GameId} from {FromVersion} to {ToVersion}: {TotalChanges} changes ({Added} added, {Modified} modified, {Deleted} deleted)",
            query.GameId, query.FromVersion, query.ToVersion,
            diff.Summary.TotalChanges, diff.Summary.Added, diff.Summary.Modified, diff.Summary.Deleted);

        return diff;
    }
}