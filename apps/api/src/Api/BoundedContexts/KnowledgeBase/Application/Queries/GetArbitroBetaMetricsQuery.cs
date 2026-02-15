using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get aggregated beta testing metrics for Arbitro Agent.
/// Issue #4328: Arbitro Agent Beta Testing - Performance Monitoring Dashboard.
/// </summary>
public record GetArbitroBetaMetricsQuery : IRequest<ArbitroBetaMetricsDto>
{
    /// <summary>
    /// Optional game session filter.
    /// </summary>
    public Guid? GameSessionId { get; init; }

    /// <summary>
    /// Optional date range filter (from).
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Optional date range filter (to).
    /// </summary>
    public DateTime? ToDate { get; init; }
}
