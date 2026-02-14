using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetProcessingMetricsQuery.
/// Retrieves aggregated processing metrics for admin monitoring.
/// Issue #4212: Admin metrics endpoint.
/// </summary>
internal sealed class GetProcessingMetricsQueryHandler : IQueryHandler<GetProcessingMetricsQuery, ProcessingMetricsDto>
{
    private readonly IProcessingMetricsService _metricsService;
    private readonly ILogger<GetProcessingMetricsQueryHandler> _logger;

    public GetProcessingMetricsQueryHandler(
        IProcessingMetricsService metricsService,
        ILogger<GetProcessingMetricsQueryHandler> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessingMetricsDto> Handle(
        GetProcessingMetricsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Retrieving aggregated processing metrics");

        // Get all step statistics
        var allStats = await _metricsService.GetAllStepStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build averages dictionary
        var averages = allStats.ToDictionary(
            kvp => kvp.Key,
            kvp => new StepAverages(
                kvp.Value.Step,
                kvp.Value.AverageDurationSeconds,
                kvp.Value.SampleSize),
            StringComparer.Ordinal);

        // Build percentiles dictionary
        var percentiles = allStats.ToDictionary(
            kvp => kvp.Key,
            kvp => new StepPercentiles(
                kvp.Value.MedianDurationSeconds,
                kvp.Value.P95DurationSeconds,
                kvp.Value.P95DurationSeconds), // P99 not yet implemented, use P95
            StringComparer.Ordinal);

        var response = new ProcessingMetricsDto(
            Averages: averages,
            Percentiles: percentiles,
            LastUpdated: DateTime.UtcNow
        );

        _logger.LogInformation("Retrieved metrics for {Count} processing steps", allStats.Count);

        return response;
    }
}
