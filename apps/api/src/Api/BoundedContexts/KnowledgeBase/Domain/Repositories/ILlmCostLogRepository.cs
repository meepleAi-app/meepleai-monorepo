using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for LLM cost log persistence
/// ISSUE-960: BGAI-018 - Cost tracking persistence layer
/// </summary>
public interface ILlmCostLogRepository
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get total costs for a date range
    /// </summary>
    Task<decimal> GetTotalCostAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get costs grouped by provider for a date range
    /// </summary>
    Task<Dictionary<string, decimal>> GetCostsByProviderAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get costs grouped by user role for a date range
    /// </summary>
    Task<Dictionary<string, decimal>> GetCostsByRoleAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get costs for a specific user
    /// </summary>
    Task<decimal> GetUserCostAsync(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get daily cost total
    /// </summary>
#pragma warning disable CA1716 // Identifiers should not match keywords - 'date' is appropriate for DateOnly parameter
    Task<decimal> GetDailyCostAsync(DateOnly date, CancellationToken cancellationToken = default);
#pragma warning restore CA1716
}

