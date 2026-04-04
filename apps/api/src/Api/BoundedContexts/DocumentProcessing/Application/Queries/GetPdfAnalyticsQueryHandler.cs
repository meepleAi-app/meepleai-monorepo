using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfAnalyticsQuery.
/// Issue #3715: Aggregated PDF analytics with role-based filtering.
/// Queries PdfDocuments and ProcessingMetrics tables for real metrics.
/// </summary>
internal class GetPdfAnalyticsQueryHandler : IRequestHandler<GetPdfAnalyticsQuery, PdfAnalyticsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPdfAnalyticsQueryHandler> _logger;

    public GetPdfAnalyticsQueryHandler(MeepleAiDbContext dbContext, ILogger<GetPdfAnalyticsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PdfAnalyticsDto> Handle(GetPdfAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = DateTime.UtcNow.AddDays(-request.TimeRangeDays);

        // Base query with optional user filtering
        var docsQuery = _dbContext.PdfDocuments.AsNoTracking().AsQueryable();
        if (!string.Equals(request.UserRole, "Admin", StringComparison.OrdinalIgnoreCase) && request.UserId.HasValue)
        {
            docsQuery = docsQuery.Where(p => p.UploadedByUserId == request.UserId.Value);
        }

        // Time-filtered query
        var timeFilteredQuery = docsQuery.Where(p => p.UploadedAt >= fromDate);

        // Counts
        var totalUploaded = await timeFilteredQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var successCount = await timeFilteredQuery.CountAsync(p => p.ProcessingState == "Ready", cancellationToken).ConfigureAwait(false);
        var failedCount = await timeFilteredQuery.CountAsync(p => p.ProcessingState == "Failed", cancellationToken).ConfigureAwait(false);
        var successRate = totalUploaded > 0 ? (decimal)successCount / totalUploaded * 100 : 0m;

        // Processing times from documents with completed processing
        var durations = await timeFilteredQuery
            .Where(p => p.ProcessingState == "Ready" && p.ProcessedAt != null)
            .Select(p => new { p.UploadedAt, p.ProcessedAt })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        TimeSpan? avgProcessingTime = null;
        TimeSpan? p95ProcessingTime = null;

        if (durations.Count > 0)
        {
            var durationSeconds = durations
                .Where(d => d.ProcessedAt.HasValue)
                .Select(d => (d.ProcessedAt!.Value - d.UploadedAt).TotalSeconds)
                .OrderBy(s => s)
                .ToList();

            if (durationSeconds.Count > 0)
            {
                avgProcessingTime = TimeSpan.FromSeconds(durationSeconds.Average());
                var p95Index = (int)Math.Ceiling(durationSeconds.Count * 0.95) - 1;
                p95ProcessingTime = TimeSpan.FromSeconds(durationSeconds[Math.Clamp(p95Index, 0, durationSeconds.Count - 1)]);
            }
        }

        // Storage breakdown - all docs (not time-filtered) for total storage
        var totalStorageBytes = await docsQuery.SumAsync(p => p.FileSizeBytes, cancellationToken).ConfigureAwait(false);

        // Storage by processing state
        var storageByState = await docsQuery
            .GroupBy(p => p.ProcessingState)
            .Select(g => new { State = g.Key, Bytes = g.Sum(p => p.FileSizeBytes) })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var storageByTier = storageByState.ToDictionary(
            s => s.State,
            s => s.Bytes,
            StringComparer.Ordinal);

        // Daily upload stats for time series
        var uploadsByDay = await timeFilteredQuery
            .GroupBy(p => p.UploadedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalCount = g.Count(),
                SuccessCount = g.Count(p => p.ProcessingState == "Ready"),
                FailedCount = g.Count(p => p.ProcessingState == "Failed")
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF Analytics: {Total} total, {Success} success, {Failed} failed in last {Days} days",
            totalUploaded, successCount, failedCount, request.TimeRangeDays);

        return new PdfAnalyticsDto
        {
            TotalUploaded = totalUploaded,
            SuccessCount = successCount,
            FailedCount = failedCount,
            SuccessRate = Math.Round(successRate, 1),
            AvgProcessingTime = avgProcessingTime,
            P95ProcessingTime = p95ProcessingTime,
            TotalStorageBytes = totalStorageBytes,
            StorageByTier = storageByTier,
            UploadsByDay = uploadsByDay.Select(d => new DailyUploadStats
            {
                Date = DateOnly.FromDateTime(d.Date),
                TotalCount = d.TotalCount,
                SuccessCount = d.SuccessCount,
                FailedCount = d.FailedCount
            }).ToList()
        };
    }
}
