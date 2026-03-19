using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;

/// <summary>
/// Handles GetAbTestQuery — returns blind session (no model info).
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed class GetAbTestQueryHandler : IQueryHandler<GetAbTestQuery, AbTestSessionDto?>
{
    private readonly IAbTestSessionRepository _repository;

    public GetAbTestQueryHandler(IAbTestSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AbTestSessionDto?> Handle(GetAbTestQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdWithVariantsAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
        return session is null ? null : AbTestMapper.ToBlindDto(session);
    }
}

/// <summary>
/// Handles RevealAbTestQuery — returns revealed session with model info (only after evaluation).
/// </summary>
internal sealed class RevealAbTestQueryHandler : IQueryHandler<RevealAbTestQuery, AbTestSessionRevealedDto?>
{
    private readonly IAbTestSessionRepository _repository;

    public RevealAbTestQueryHandler(IAbTestSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AbTestSessionRevealedDto?> Handle(RevealAbTestQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdWithVariantsAsync(query.SessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
            return null;

        if (session.Status != AbTestStatus.Evaluated)
            throw new InvalidOperationException("Cannot reveal models before evaluation is complete");

        return AbTestMapper.ToRevealedDto(session);
    }
}

/// <summary>
/// Handles GetAbTestsQuery — paginated list.
/// </summary>
internal sealed class GetAbTestsQueryHandler : IQueryHandler<GetAbTestsQuery, AbTestSessionListDto>
{
    private readonly IAbTestSessionRepository _repository;

    public GetAbTestsQueryHandler(IAbTestSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AbTestSessionListDto> Handle(GetAbTestsQuery query, CancellationToken cancellationToken)
    {
        AbTestStatus? status = query.Status is not null && Enum.TryParse<AbTestStatus>(query.Status, true, out var parsed)
            ? parsed
            : null;

        var skip = (query.Page - 1) * query.PageSize;

        var items = await _repository.GetByUserAsync(query.UserId, status, skip, query.PageSize, cancellationToken).ConfigureAwait(false);
        var totalCount = await _repository.CountByUserAsync(query.UserId, status, cancellationToken).ConfigureAwait(false);

        return new AbTestSessionListDto(
            Items: items.Select(AbTestMapper.ToBlindDto).ToList(),
            TotalCount: totalCount,
            Page: query.Page,
            PageSize: query.PageSize);
    }
}

/// <summary>
/// Handles GetAbTestAnalyticsQuery — aggregated stats.
/// </summary>
internal sealed class GetAbTestAnalyticsQueryHandler : IQueryHandler<GetAbTestAnalyticsQuery, AbTestAnalyticsDto>
{
    private readonly IAbTestSessionRepository _repository;

    public GetAbTestAnalyticsQueryHandler(IAbTestSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AbTestAnalyticsDto> Handle(GetAbTestAnalyticsQuery query, CancellationToken cancellationToken)
    {
        // Get all evaluated sessions for analytics (admin-level, no user filter)
        var allSessions = await _repository.GetAllEvaluatedWithVariantsAsync(cancellationToken).ConfigureAwait(false);

        // Filter by date range
        if (query.DateFrom.HasValue)
            allSessions = allSessions.Where(s => s.CreatedAt >= query.DateFrom.Value).ToList();
        if (query.DateTo.HasValue)
            allSessions = allSessions.Where(s => s.CreatedAt <= query.DateTo.Value).ToList();

        // Calculate win rates
        var winCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in allSessions)
        {
            var winner = session.GetWinner();
            foreach (var variant in session.Variants.Where(v => v.Evaluation is not null))
            {
                totalCounts.TryGetValue(variant.ModelId, out var total);
                totalCounts[variant.ModelId] = total + 1;

                if (winner is not null && variant.Id == winner.Id)
                {
                    winCounts.TryGetValue(variant.ModelId, out var wins);
                    winCounts[variant.ModelId] = wins + 1;
                }
            }
        }

        var winRates = totalCounts.Select(kvp =>
        {
            winCounts.TryGetValue(kvp.Key, out var wins);
            return new ModelWinRateDto(kvp.Key, wins, kvp.Value, kvp.Value > 0 ? (decimal)wins / kvp.Value : 0);
        }).OrderByDescending(w => w.WinRate).ToList();

        // Calculate average scores per model
        var modelEvals = allSessions
            .SelectMany(s => s.Variants)
            .Where(v => v.Evaluation is not null)
            .GroupBy(v => v.ModelId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ModelAvgScoreDto(
                g.Key,
                (decimal)g.Average(v => v.Evaluation!.Accuracy),
                (decimal)g.Average(v => v.Evaluation!.Completeness),
                (decimal)g.Average(v => v.Evaluation!.Clarity),
                (decimal)g.Average(v => v.Evaluation!.Tone),
                g.Average(v => v.Evaluation!.AverageScore),
                g.Count()))
            .OrderByDescending(m => m.AvgOverall)
            .ToList();

        return new AbTestAnalyticsDto
        {
            TotalTests = allSessions.Count,
            CompletedTests = allSessions.Count(s => s.Status == AbTestStatus.Evaluated),
            TotalCost = allSessions.Sum(s => s.TotalCost),
            ModelWinRates = winRates,
            ModelAvgScores = modelEvals
        };
    }
}
