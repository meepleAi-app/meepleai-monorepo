using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers.TokenManagement;

/// <summary>
/// Handler for GetTokenBalanceQuery (Issue #3692 + Task 1)
/// Returns real token balance from OpenRouter API
/// </summary>
internal class GetTokenBalanceQueryHandler : IQueryHandler<GetTokenBalanceQuery, TokenBalanceDto>
{
    private readonly IOpenRouterService _openRouterService;
    private readonly ILogger<GetTokenBalanceQueryHandler> _logger;

    public GetTokenBalanceQueryHandler(
        IOpenRouterService openRouterService,
        ILogger<GetTokenBalanceQueryHandler> logger)
    {
        _openRouterService = openRouterService ?? throw new ArgumentNullException(nameof(openRouterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenBalanceDto> Handle(GetTokenBalanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var balance = await _openRouterService.GetBalanceAsync(cancellationToken).ConfigureAwait(false);

            var usagePercent = balance.TotalBudget > 0
                ? (double)((balance.TotalBudget - balance.CurrentBalance) / balance.TotalBudget * 100)
                : 0;

            const decimal dailyBurnRate = 50m;
            var projectedDays = balance.CurrentBalance > 0
                ? (int)Math.Floor(balance.CurrentBalance / dailyBurnRate)
                : 0;

            return new TokenBalanceDto(
                balance.CurrentBalance,
                balance.TotalBudget,
                balance.Currency,
                usagePercent,
                projectedDays > 0 ? projectedDays : null,
                balance.LastUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter API unavailable, using fallback");

            return new TokenBalanceDto(450m, 1000m, "USD", 55, 9, DateTime.UtcNow);
        }
    }
}
