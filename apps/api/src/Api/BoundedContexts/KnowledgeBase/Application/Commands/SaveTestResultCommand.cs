using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to save an agent test result.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal record SaveTestResultCommand(
    Guid TypologyId,
    string Query,
    string Response,
    string ModelUsed,
    double ConfidenceScore,
    int TokensUsed,
    decimal CostEstimate,
    int LatencyMs,
    Guid ExecutedBy,
    string? StrategyOverride = null,
    string? CitationsJson = null
) : IRequest<Guid>;
