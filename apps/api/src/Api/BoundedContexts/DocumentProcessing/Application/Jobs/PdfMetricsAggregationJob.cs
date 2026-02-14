using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Background job to aggregate PDF processing metrics daily.
/// Issue #3715.5: Populates PdfProcessingMetrics table for analytics dashboard.
/// Runs daily at midnight UTC.
/// </summary>
[DisallowConcurrentExecution]
public class PdfMetricsAggregationJob : IJob
{
    private readonly ILogger<PdfMetricsAggregationJob> _logger;

    public PdfMetricsAggregationJob(ILogger<PdfMetricsAggregationJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("PDF Metrics Aggregation Job started");

        try
        {
            // NOTE: Placeholder implementation - full metrics aggregation will be implemented
            // when PdfProcessingMetrics table schema is finalized (Issue #3715.5)
            // Future: Query pdf_documents, calculate aggregates, insert into metrics table

            await Task.CompletedTask.ConfigureAwait(false);

            _logger.LogInformation("PDF Metrics Aggregation Job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF Metrics Aggregation Job failed");
            throw;
        }
    }
}
