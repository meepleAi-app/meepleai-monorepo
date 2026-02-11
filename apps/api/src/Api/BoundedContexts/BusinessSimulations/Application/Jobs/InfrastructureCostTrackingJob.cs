using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.BusinessSimulations.Application.Jobs;

/// <summary>
/// Daily background job that aggregates previous day's LLM API costs
/// and creates an Infrastructure expense entry in the financial ledger.
/// Runs daily at 05:00 UTC (after achievement evaluation at 04:00 UTC).
/// Issue #3721: Automatic Ledger Tracking (Epic #3688)
/// </summary>
[DisallowConcurrentExecution]
internal sealed class InfrastructureCostTrackingJob : IJob
{
    private readonly ILedgerTrackingService _ledgerTrackingService;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<InfrastructureCostTrackingJob> _logger;

    public InfrastructureCostTrackingJob(
        ILedgerTrackingService ledgerTrackingService,
        ILlmCostLogRepository costLogRepository,
        ILogger<InfrastructureCostTrackingJob> logger)
    {
        _ledgerTrackingService = ledgerTrackingService ?? throw new ArgumentNullException(nameof(ledgerTrackingService));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation("Starting daily infrastructure cost tracking job");

        try
        {
            // Get yesterday's date (the job runs at 05:00, tracking previous day)
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

            // Aggregate total LLM API costs for the previous day
            var dailyCost = await _costLogRepository.GetDailyCostAsync(yesterday, cancellationToken)
                .ConfigureAwait(false);

            if (dailyCost <= 0)
            {
                _logger.LogInformation(
                    "No LLM costs recorded for {Date}, skipping ledger entry",
                    yesterday);

                context.Result = new { Success = true, Date = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Cost = 0m, Skipped = true };
                return;
            }

            // Get cost breakdown by provider for metadata
            var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(
                yesterday, yesterday, cancellationToken).ConfigureAwait(false);

            var metadata = JsonSerializer.Serialize(new
            {
                date = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                totalCostUsd = dailyCost,
                providers = costsByProvider,
                generatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            });

            // Create the infrastructure cost ledger entry
            var entryDate = yesterday.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            await _ledgerTrackingService.TrackInfrastructureCostAsync(
                amount: dailyCost,
                description: $"Daily LLM API costs for {yesterday:yyyy-MM-dd}",
                metadata: metadata,
                date: entryDate,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Infrastructure cost ledger entry created: Date={Date}, Cost=${Cost}, Providers={Providers}",
                yesterday, dailyCost, costsByProvider.Count);

            context.Result = new
            {
                Success = true,
                Date = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Cost = dailyCost,
                Providers = costsByProvider.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track infrastructure costs");
            context.Result = new { Success = false, Error = ex.Message };
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
