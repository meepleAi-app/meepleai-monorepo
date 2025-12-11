using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
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
