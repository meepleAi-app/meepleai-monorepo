using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Implementation of processing metrics service using EF Core.
/// Issue #4212: Historical metrics and data-driven ETA calculation.
/// </summary>
internal sealed class ProcessingMetricsService : IProcessingMetricsService
{
    private readonly MeepleAiDbContext _context;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ILogger<ProcessingMetricsService> _logger;

    // Minimum samples required for reliable statistics
    private const int MinSampleSize = 10;

    public ProcessingMetricsService(
        MeepleAiDbContext context,
        IPdfDocumentRepository pdfRepository,
        ILogger<ProcessingMetricsService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordStepDurationAsync(
        Guid pdfId,
        PdfProcessingState step,
        TimeSpan duration,
        long pdfSizeBytes,
        int pageCount,
        CancellationToken cancellationToken = default)
    {

        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));

        if (pdfSizeBytes <= 0)
            throw new ArgumentException("PDF size must be positive", nameof(pdfSizeBytes));

        if (pageCount <= 0)
            throw new ArgumentException("Page count must be positive", nameof(pageCount));

        var metric = new ProcessingMetricEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            Step = step.ToString(),
            DurationSeconds = (decimal)duration.TotalSeconds,
            PdfSizeBytes = pdfSizeBytes,
            PageCount = pageCount,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProcessingMetrics.Add(metric);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Recorded metric: PdfId={PdfId}, Step={Step}, Duration={Duration}s, Size={Size}bytes, Pages={Pages}",
            pdfId, step, duration.TotalSeconds, pdfSizeBytes, pageCount);
    }

    public async Task<StepDurationStats> GetAverageDurationAsync(
        PdfProcessingState step,
        CancellationToken cancellationToken = default)
    {

        var stepName = step.ToString();

        // Get last 100 records for the step
        var metrics = await _context.ProcessingMetrics
            .Where(m => m.Step == stepName)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .Select(m => m.DurationSeconds)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (metrics.Count == 0)
        {
            _logger.LogWarning("No historical data for step {Step}", step);
            return new StepDurationStats(stepName, 0, 0, 0, 0);
        }

        var durations = metrics.Select(m => (double)m).OrderBy(d => d).ToList();

        var average = durations.Average();
        var median = CalculatePercentile(durations, 0.50);
        var p95 = CalculatePercentile(durations, 0.95);

        return new StepDurationStats(stepName, average, median, p95, metrics.Count);
    }

    public async Task<TimeSpan?> CalculateETAAsync(
        Guid pdfId,
        PdfProcessingState currentStep,
        CancellationToken cancellationToken = default)
    {
        // Terminal states have no ETA
        if (currentStep == PdfProcessingState.Ready || currentStep == PdfProcessingState.Failed)
            return TimeSpan.Zero;

        // Get PDF metadata
        var pdf = await _pdfRepository.GetByIdAsync(pdfId, cancellationToken).ConfigureAwait(false);
        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for ETA calculation", pdfId);
            return null;
        }

        // Calculate remaining steps
        var remainingSteps = GetRemainingSteps(currentStep);
        if (remainingSteps.Count == 0)
            return TimeSpan.Zero;

        // Get historical averages for remaining steps
        double totalEstimatedSeconds = 0;
        int stepsWithData = 0;

        foreach (var step in remainingSteps)
        {
            var stats = await GetAverageDurationAsync(step, cancellationToken).ConfigureAwait(false);

            if (stats.SampleSize >= MinSampleSize)
            {
                // Use average duration adjusted for PDF characteristics
                var adjustedDuration = await AdjustForPdfCharacteristics(
                    stats.AverageDurationSeconds,
                    pdf.FileSize.Bytes,
                    pdf.PageCount ?? 10,
                    stats).ConfigureAwait(false);

                totalEstimatedSeconds += adjustedDuration;
                stepsWithData++;
            }
            else
            {
                // Fallback to static calculation (2 seconds per page per step)
                totalEstimatedSeconds += 2.0 * (pdf.PageCount ?? 10);
            }
        }

        if (stepsWithData == 0)
        {
            _logger.LogInformation(
                "Insufficient historical data for ETA, using static calculation for PDF {PdfId}",
                pdfId);
        }

        return TimeSpan.FromSeconds(totalEstimatedSeconds);
    }

    public async Task<Dictionary<string, StepDurationStats>> GetAllStepStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var steps = Enum.GetValues<PdfProcessingState>()
            .Where(s => s != PdfProcessingState.Pending &&
                       s != PdfProcessingState.Ready &&
                       s != PdfProcessingState.Failed)
            .ToList();

        var statistics = new Dictionary<string, StepDurationStats>(StringComparer.Ordinal);

        foreach (var step in steps)
        {
            var stats = await GetAverageDurationAsync(step, cancellationToken).ConfigureAwait(false);
            statistics[step.ToString()] = stats;
        }

        return statistics;
    }

    public async Task<int> CleanupOldMetricsAsync(
        int retainPerStep = 1000,
        CancellationToken cancellationToken = default)
    {
        if (retainPerStep <= 0)
            throw new ArgumentException("Retain count must be positive", nameof(retainPerStep));

        _logger.LogInformation("Starting metrics cleanup, retaining {RetainCount} per step", retainPerStep);

        // Get all step names
        var steps = await _context.ProcessingMetrics
            .Select(m => m.Step)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int totalDeleted = 0;

        foreach (var step in steps)
        {
            // Get IDs to delete (older than the last N records)
            var idsToDelete = await _context.ProcessingMetrics
                .Where(m => m.Step == step)
                .OrderByDescending(m => m.CreatedAt)
                .Skip(retainPerStep)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (idsToDelete.Count > 0)
            {
                // Delete in batches of 1000 to avoid transaction timeout
                for (int i = 0; i < idsToDelete.Count; i += 1000)
                {
                    var batch = idsToDelete.Skip(i).Take(1000).ToList();
                    await _context.ProcessingMetrics
                        .Where(m => batch.Contains(m.Id))
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                totalDeleted += idsToDelete.Count;
                _logger.LogDebug("Deleted {Count} old metrics for step {Step}", idsToDelete.Count, step);
            }
        }

        _logger.LogInformation("Metrics cleanup completed, deleted {Total} records", totalDeleted);
        return totalDeleted;
    }

    /// <summary>
    /// Gets remaining processing steps from current state.
    /// </summary>
    private static List<PdfProcessingState> GetRemainingSteps(PdfProcessingState currentStep)
    {
        var allSteps = new List<PdfProcessingState>
        {
            PdfProcessingState.Uploading,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            PdfProcessingState.Embedding,
            PdfProcessingState.Indexing
        };

        var currentIndex = allSteps.IndexOf(currentStep);
        return currentIndex >= 0 ? allSteps.Skip(currentIndex + 1).ToList() : new List<PdfProcessingState>();
    }

    /// <summary>
    /// Adjusts duration based on PDF size and page count relative to historical averages.
    /// </summary>
    private async Task<double> AdjustForPdfCharacteristics(
        double baseDuration,
        long pdfSizeBytes,
        int pageCount,
        StepDurationStats stats)
    {
        // Get average PDF size and page count from historical data
        var stepName = stats.Step;
        var avgMetrics = await _context.ProcessingMetrics
            .Where(m => m.Step == stepName)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .Select(m => new { m.PdfSizeBytes, m.PageCount })
            .ToListAsync()
            .ConfigureAwait(false);

        if (avgMetrics.Count == 0)
            return baseDuration;

        var avgSize = avgMetrics.Average(m => m.PdfSizeBytes);
        var avgPages = avgMetrics.Average(m => m.PageCount);

        // Adjustment factor based on size and page count (capped at 3x for outliers)
        var sizeFactor = Math.Min(pdfSizeBytes / avgSize, 3.0);
        var pageFactor = Math.Min(pageCount / avgPages, 3.0);

        // Weighted average: 60% size, 40% pages
        var adjustmentFactor = (sizeFactor * 0.6) + (pageFactor * 0.4);

        return baseDuration * adjustmentFactor;
    }

    /// <summary>
    /// Calculates percentile from sorted duration list.
    /// </summary>
    private static double CalculatePercentile(List<double> sortedDurations, double percentile)
    {
        if (sortedDurations.Count == 0)
            return 0;

        if (sortedDurations.Count == 1)
            return sortedDurations[0];

        var index = (sortedDurations.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedDurations[lowerIndex];

        var lowerValue = sortedDurations[lowerIndex];
        var upperValue = sortedDurations[upperIndex];
        var fraction = index - lowerIndex;

        return lowerValue + ((upperValue - lowerValue) * fraction);
    }
}
