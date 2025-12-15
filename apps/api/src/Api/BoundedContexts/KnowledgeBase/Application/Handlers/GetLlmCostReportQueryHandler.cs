using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetLlmCostReportQuery
/// ISSUE-960: BGAI-018 - Cost reporting handler
/// </summary>
internal class GetLlmCostReportQueryHandler : IRequestHandler<GetLlmCostReportQuery, LlmCostReportDto>
{
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<GetLlmCostReportQueryHandler> _logger;

    // Threshold from ADR-004: Alert if daily cost exceeds $100
    private const decimal DailyAlertThreshold = 100m;

    public GetLlmCostReportQueryHandler(
        ILlmCostLogRepository costLogRepository,
        ILogger<GetLlmCostReportQueryHandler> logger)
    {
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmCostReportDto> Handle(GetLlmCostReportQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation(
            "Generating LLM cost report for {StartDate} to {EndDate} (UserId: {UserId})",
            request.StartDate, request.EndDate, request.UserId);

        // Get total cost
        var totalCost = request.UserId.HasValue
            ? await _costLogRepository.GetUserCostAsync(request.UserId.Value, request.StartDate, request.EndDate, ct)
.ConfigureAwait(false) : await _costLogRepository.GetTotalCostAsync(request.StartDate, request.EndDate, ct).ConfigureAwait(false);

        // Get costs by provider
        var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(request.StartDate, request.EndDate, ct).ConfigureAwait(false);

        // Get costs by role
        var costsByRole = await _costLogRepository.GetCostsByRoleAsync(request.StartDate, request.EndDate, ct).ConfigureAwait(false);

        // Get today's cost for threshold check
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyCost = await _costLogRepository.GetDailyCostAsync(today, ct).ConfigureAwait(false);
        var exceedsThreshold = dailyCost > DailyAlertThreshold;

        if (exceedsThreshold)
        {
            _logger.LogWarning(
                "Daily LLM cost ${DailyCost:F2} exceeds threshold ${Threshold:F2}",
                dailyCost, DailyAlertThreshold);
        }

        var report = new LlmCostReportDto
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalCost = totalCost,
            CostsByProvider = costsByProvider,
            CostsByRole = costsByRole,
            DailyCost = dailyCost,
            ExceedsThreshold = exceedsThreshold,
            ThresholdAmount = DailyAlertThreshold
        };

        _logger.LogInformation(
            "Cost report generated: Total=${Total:F6}, Daily=${Daily:F6}, Threshold exceeded={Exceeds}",
            totalCost, dailyCost, exceedsThreshold);

        return report;
    }
}
