using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for LLM cost log persistence
/// ISSUE-960: BGAI-018 - Cost tracking persistence layer
/// </summary>
internal interface ILlmCostLogRepository
{
    /// <summary>
    /// Log an LLM request cost
    /// </summary>
    Task LogCostAsync(
        Guid? userId,
        string userRole,
        LlmCostCalculation cost,
        string endpoint,
        bool success,
        string? errorMessage,
        int latencyMs,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// Get total costs for a date range
    /// </summary>
    Task<decimal> GetTotalCostAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Get costs grouped by provider for a date range
    /// </summary>
    Task<Dictionary<string, decimal>> GetCostsByProviderAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Get costs grouped by user role for a date range
    /// </summary>
    Task<Dictionary<string, decimal>> GetCostsByRoleAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Get costs for a specific user
    /// </summary>
    Task<decimal> GetUserCostAsync(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Get daily cost total
    /// </summary>
    Task<decimal> GetDailyCostAsync(DateOnly date, CancellationToken ct = default);
}
