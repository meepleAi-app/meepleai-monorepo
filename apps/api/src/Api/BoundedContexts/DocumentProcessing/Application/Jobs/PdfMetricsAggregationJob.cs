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

    public Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("PDF Metrics Aggregation Job started");

        try
        {
            // FUTURE: Aggregate yesterday's metrics (Issue #3715.5)
            // - Query pdf_documents WHERE DATE(uploadedAt) = yesterday
            // - Calculate: total, success, failed, avg time, storage
            // - INSERT INTO pdf_processing_metrics

            _logger.LogInformation("PDF Metrics Aggregation Job completed successfully (stub)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF Metrics Aggregation Job failed");
            throw;
        }

        return Task.CompletedTask;
    }
}
