using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for AgentTestResult persistence.
/// Issue #3379 - Agent Test Results History &amp; Persistence
/// </summary>
internal interface IAgentTestResultRepository
{
    /// <summary>
    /// Gets a test result by ID.
    /// </summary>
    Task<AgentTestResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets test results for a specific typology.
    /// </summary>
    Task<List<AgentTestResult>> GetByTypologyIdAsync(
        Guid typologyId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets test results executed by a specific user.
    /// </summary>
    Task<List<AgentTestResult>> GetByExecutedByAsync(
        Guid userId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets test results within a date range.
    /// </summary>
    Task<List<AgentTestResult>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets saved/favorited test results.
    /// </summary>
    Task<List<AgentTestResult>> GetSavedAsync(
        Guid? userId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of test results for a typology.
    /// </summary>
    Task<int> GetCountByTypologyIdAsync(Guid typologyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new test result.
    /// </summary>
    Task AddAsync(AgentTestResult testResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing test result.
    /// </summary>
    Task UpdateAsync(AgentTestResult testResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a test result.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a test result exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all test results for metrics aggregation with optional filters.
    /// Issue #3382: Agent Metrics Dashboard.
    /// </summary>
    Task<List<AgentTestResult>> GetForMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        Guid? typologyId = null,
        string? strategy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate metrics counts efficiently.
    /// Issue #3382: Agent Metrics Dashboard.
    /// </summary>
    Task<(int TotalCount, int TotalTokens, decimal TotalCost, double AvgLatency, double AvgConfidence)> GetAggregateMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        Guid? typologyId = null,
        CancellationToken cancellationToken = default);
}
