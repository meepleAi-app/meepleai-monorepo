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
            // TODO: Aggregate yesterday's metrics
            // - Query pdf_documents WHERE DATE(uploadedAt) = yesterday
            // - Calculate: total, success, failed, avg time, storage
            // - INSERT INTO pdf_processing_metrics

            await Task.CompletedTask; // CS1998: Satisfy async requirement

            _logger.LogInformation("PDF Metrics Aggregation Job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF Metrics Aggregation Job failed");
            throw;
        }
    }
}
