namespace Api.BoundedContexts.Administration.Application.Interfaces;

/// <summary>
/// Service for OpenRouter API integration (Issue #3692 - Task 1)
/// </summary>
public interface IOpenRouterService
{
    Task<OpenRouterBalanceResponse> GetBalanceAsync(CancellationToken cancellationToken = default);
    Task AddCreditsAsync(decimal amount, string currency, CancellationToken cancellationToken = default);
}

public sealed record OpenRouterBalanceResponse(
    decimal CurrentBalance,
    decimal TotalBudget,
    string Currency,
    DateTime LastUpdated);
