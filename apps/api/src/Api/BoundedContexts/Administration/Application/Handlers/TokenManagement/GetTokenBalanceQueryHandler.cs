using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers.TokenManagement;

/// <summary>
/// Handler for GetTokenBalanceQuery (Issue #3692)
/// Returns current token balance and projection data
/// </summary>
internal class GetTokenBalanceQueryHandler : IQueryHandler<GetTokenBalanceQuery, TokenBalanceDto>
{
    private readonly ILogger<GetTokenBalanceQueryHandler> _logger;

    public GetTokenBalanceQueryHandler(ILogger<GetTokenBalanceQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenBalanceDto> Handle(GetTokenBalanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Mock implementation - will integrate with OpenRouter API later
        _logger.LogInformation("Retrieving token balance (mock data)");

        await Task.CompletedTask.ConfigureAwait(false);

        const decimal currentBalance = 450m;
        const decimal totalBudget = 1000m;
        const string currency = "EUR";

        var usagePercent = (double)((totalBudget - currentBalance) / totalBudget * 100);

        // Simple projection: if usage continues at current rate
        // Mock: assume 50 EUR/day burn rate
        const decimal dailyBurnRate = 50m;
        var projectedDays = currentBalance > 0
            ? (int)Math.Floor(currentBalance / dailyBurnRate)
            : 0;

        return new TokenBalanceDto(
            CurrentBalance: currentBalance,
            TotalBudget: totalBudget,
            Currency: currency,
            UsagePercent: usagePercent,
            ProjectedDaysUntilDepletion: projectedDays,
            LastUpdated: DateTime.UtcNow);
    }
}
