using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Handler for GetTokenConsumptionQuery (Issue #3692)
/// Returns daily token consumption trend data
/// </summary>
internal class GetTokenConsumptionQueryHandler : IQueryHandler<GetTokenConsumptionQuery, TokenConsumptionDataDto>
{
    private readonly IUserTokenUsageRepository _usageRepository;
    private readonly ILogger<GetTokenConsumptionQueryHandler> _logger;

    public GetTokenConsumptionQueryHandler(
        IUserTokenUsageRepository usageRepository,
        ILogger<GetTokenConsumptionQueryHandler> logger)
    {
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenConsumptionDataDto> Handle(GetTokenConsumptionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var days = Math.Max(1, Math.Min(query.Days, 365)); // Clamp between 1-365 days
        var fromDate = DateTime.UtcNow.AddDays(-days);
        var untilDate = DateTime.UtcNow;

        _logger.LogInformation("Retrieving token consumption for last {Days} days", days);

        var usageHistory = await _usageRepository
            .GetUsageHistoryAsync(fromDate, untilDate, cancellationToken)
            .ConfigureAwait(false);

        // Convert to DTO points
        var points = usageHistory
            .OrderBy(h => h.Date)
            .Select(h => new TokenConsumptionPointDto(
                Date: h.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                Tokens: h.TotalTokens,
                Cost: h.TotalCost))
            .ToList();

        // Calculate aggregates
        var totalTokens = usageHistory.Sum(h => h.TotalTokens);
        var totalCost = usageHistory.Sum(h => h.TotalCost);
        var avgDailyTokens = usageHistory.Count > 0
            ? (int)Math.Round((double)totalTokens / usageHistory.Count)
            : 0;
        var avgDailyCost = usageHistory.Count > 0
            ? totalCost / usageHistory.Count
            : 0m;

        return new TokenConsumptionDataDto(
            Points: points,
            TotalTokens: totalTokens,
            TotalCost: totalCost,
            AvgDailyTokens: avgDailyTokens,
            AvgDailyCost: avgDailyCost);
    }
}
