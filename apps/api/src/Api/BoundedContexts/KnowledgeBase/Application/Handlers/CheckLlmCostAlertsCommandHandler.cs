using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles LLM cost alert checking.
/// ISSUE-960: BGAI-018 - Cost monitoring and alerting
/// </summary>
public class CheckLlmCostAlertsCommandHandler : ICommandHandler<CheckLlmCostAlertsCommand, CheckLlmCostAlertsResult>
{
    private readonly LlmCostAlertService _alertService;

    public CheckLlmCostAlertsCommandHandler(LlmCostAlertService alertService)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    public async Task<CheckLlmCostAlertsResult> Handle(CheckLlmCostAlertsCommand command, CancellationToken cancellationToken)
    {
        // Check all thresholds (daily, weekly, monthly projection)
        await _alertService.CheckDailyCostThresholdAsync(cancellationToken);
        await _alertService.CheckWeeklyCostThresholdAsync(cancellationToken);
        await _alertService.CheckMonthlyCostProjectionAsync(cancellationToken);

        return new CheckLlmCostAlertsResult(
            Success: true,
            Message: "Cost threshold checks completed"
        );
    }
}
