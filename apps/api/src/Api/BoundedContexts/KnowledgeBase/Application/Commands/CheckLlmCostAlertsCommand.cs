using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to check LLM cost thresholds and trigger alerts if needed.
/// ISSUE-960: BGAI-018 - Cost monitoring and alerting
/// </summary>
public record CheckLlmCostAlertsCommand() : ICommand<CheckLlmCostAlertsResult>;

/// <summary>
/// Result of cost alert check operation.
/// </summary>
public record CheckLlmCostAlertsResult(
    bool Success,
    string Message
);
