using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.Services;

/// <summary>
/// ADMIN-02: Service for retrieving analytics dashboard statistics.
/// Provides aggregated metrics from database with HybridCache optimization.
/// </summary>
internal class AdminStatsService : IAdminStatsService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ILogger<AdminStatsService> _logger;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1); // Issue #879: Reduced from 5min to 1min for dashboard stats

    public AdminStatsService(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILogger<AdminStatsService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Get comprehensive dashboard statistics with caching.
    /// Cache key includes query parameters for proper cache invalidation.
    /// </summary>
    public async Task<DashboardStatsDto> GetDashboardStatsAsync(
        AnalyticsQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:stats:{queryParams.Days}:{queryParams.GameId}:{queryParams.RoleFilter}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for dashboard stats, generating fresh data");

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var fromDate = queryParams.FromDate ?? now.AddDays(-queryParams.Days);
                var toDate = queryParams.ToDate ?? now;

                // Parallel execution of independent queries for performance
                var metricsTask = GetMetricsAsync(now, cancellationToken);
                var userTrendTask = GetUserTrendAsync(fromDate, toDate, queryParams.RoleFilter, cancellationToken);
                var sessionTrendTask = GetSessionTrendAsync(fromDate, toDate, cancellationToken);
                var apiRequestTrendTask = GetApiRequestTrendAsync(fromDate, toDate, queryParams.GameId, cancellationToken);
                var pdfUploadTrendTask = GetPdfUploadTrendAsync(fromDate, toDate, queryParams.GameId, cancellationToken);
                var chatMessageTrendTask = GetChatMessageTrendAsync(fromDate, toDate, cancellationToken);

                await Task.WhenAll(metricsTask, userTrendTask, sessionTrendTask,
                    apiRequestTrendTask, pdfUploadTrendTask, chatMessageTrendTask).ConfigureAwait(false);

                return new DashboardStatsDto(
                    Metrics: await metricsTask.ConfigureAwait(false),
                    UserTrend: await userTrendTask.ConfigureAwait(false),
                    SessionTrend: await sessionTrendTask.ConfigureAwait(false),
                    ApiRequestTrend: await apiRequestTrendTask.ConfigureAwait(false),
                    PdfUploadTrend: await pdfUploadTrendTask.ConfigureAwait(false),
                    ChatMessageTrend: await chatMessageTrendTask.ConfigureAwait(false),
                    GeneratedAt: _timeProvider.GetUtcNow().UtcDateTime
                );
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromSeconds(30) // Issue #879: Proportional to 1min L2 (was 2min for 5min L2)
            },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Get current dashboard metrics (totals and aggregates).
    /// Uses AsNoTracking for read-only performance optimization (PERF-06).
    /// </summary>
    private async Task<DashboardMetrics> GetMetricsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var startOfDay = now.Date;
        var start7DaysAgo = now.AddDays(-7);
        var start30DaysAgo = now.AddDays(-30);

        // Parallel independent queries (original 8 + new 8 for Issue #874)
        var totalUsersTask = _dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeSessionsTask = _dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
            .CountAsync(cancellationToken);

        var apiRequestsTodayTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= startOfDay)
            .CountAsync(cancellationToken);

        var totalPdfDocumentsTask = _dbContext.PdfDocuments
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalChatMessagesTask = _dbContext.ChatLogs
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var avgConfidenceTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.Confidence.HasValue)
            .AverageAsync(log => log.Confidence, cancellationToken);

        var totalRagRequestsTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalTokensTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .SumAsync(log => (long)log.TokenCount, cancellationToken);

        // Issue #874: Additional metrics for centralized dashboard
        var totalGamesTask = _dbContext.Games
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var apiRequests7dTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= start7DaysAgo)
            .CountAsync(cancellationToken);

        var apiRequests30dTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= start30DaysAgo)
            .CountAsync(cancellationToken);

        var avgLatency24hTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= startOfDay)
            .AverageAsync(log => (double?)log.LatencyMs, cancellationToken);

        var avgLatency7dTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= start7DaysAgo)
            .AverageAsync(log => (double?)log.LatencyMs, cancellationToken);

        var errorCount24hTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= startOfDay && log.Status != "Success")
            .CountAsync(cancellationToken);

        var totalCount24hTask = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= startOfDay)
            .CountAsync(cancellationToken);

        var activeAlertsTask = _dbContext.Alerts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .CountAsync(cancellationToken);

        var resolvedAlertsTask = _dbContext.Alerts
            .AsNoTracking()
            .Where(a => !a.IsActive && a.ResolvedAt.HasValue)
            .CountAsync(cancellationToken);

        await Task.WhenAll(
            totalUsersTask, activeSessionsTask, apiRequestsTodayTask,
            totalPdfDocumentsTask, totalChatMessagesTask, avgConfidenceTask,
            totalRagRequestsTask, totalTokensTask,
            // Issue #874 metrics
            totalGamesTask, apiRequests7dTask, apiRequests30dTask,
            avgLatency24hTask, avgLatency7dTask, errorCount24hTask, totalCount24hTask,
            activeAlertsTask, resolvedAlertsTask
        ).ConfigureAwait(false);

        // Calculate error rate (avoid division by zero)
        var errorCount = await errorCount24hTask.ConfigureAwait(false);
        var totalCount = await totalCount24hTask.ConfigureAwait(false);
        var errorRate = totalCount > 0 ? (double)errorCount / totalCount : 0.0;

        return new DashboardMetrics(
            TotalUsers: await totalUsersTask.ConfigureAwait(false),
            ActiveSessions: await activeSessionsTask.ConfigureAwait(false),
            ApiRequestsToday: await apiRequestsTodayTask.ConfigureAwait(false),
            TotalPdfDocuments: await totalPdfDocumentsTask.ConfigureAwait(false),
            TotalChatMessages: await totalChatMessagesTask.ConfigureAwait(false),
            AverageConfidenceScore: await avgConfidenceTask.ConfigureAwait(false) ?? 0.0,
            TotalRagRequests: await totalRagRequestsTask.ConfigureAwait(false),
            TotalTokensUsed: await totalTokensTask.ConfigureAwait(false),
            // Issue #874: Additional metrics
            TotalGames: await totalGamesTask.ConfigureAwait(false),
            ApiRequests7d: await apiRequests7dTask.ConfigureAwait(false),
            ApiRequests30d: await apiRequests30dTask.ConfigureAwait(false),
            AverageLatency24h: await avgLatency24hTask.ConfigureAwait(false) ?? 0.0,
            AverageLatency7d: await avgLatency7dTask.ConfigureAwait(false) ?? 0.0,
            ErrorRate24h: errorRate,
            ActiveAlerts: await activeAlertsTask.ConfigureAwait(false),
            ResolvedAlerts: await resolvedAlertsTask
.ConfigureAwait(false));
    }

    /// <summary>
    /// Get user registration trend over time with optional role filter.
    /// Groups by date for efficient time-series aggregation.
    /// </summary>
    private async Task<IReadOnlyList<TimeSeriesDataPoint>> GetUserTrendAsync(
        DateTime fromDate,
        DateTime toDate,
        string? roleFilter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.CreatedAt >= fromDate && u.CreatedAt <= toDate);

        // Apply role filter if specified
        if (!string.IsNullOrWhiteSpace(roleFilter) && !string.Equals(roleFilter, "all", StringComparison.Ordinal))
        {
            var normalizedRole = roleFilter.ToLower(CultureInfo.InvariantCulture);
            query = query.Where(u => u.Role == normalizedRole);
        }

        var data = await query
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.LongCount()
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return FillMissingDates(
            data.Select(d => new TimeSeriesDataPoint(d.Date, d.Count, null)).ToList(),
            fromDate,
            toDate);

    }

    /// <summary>
    /// Get session creation trend over time.
    /// </summary>
    private async Task<IReadOnlyList<TimeSeriesDataPoint>> GetSessionTrendAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var data = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.CreatedAt >= fromDate && s.CreatedAt <= toDate)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.LongCount()
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return FillMissingDates(
            data.Select(d => new TimeSeriesDataPoint(d.Date, d.Count, null)).ToList(),
            fromDate,
            toDate);
    }

    /// <summary>
    /// Get API request trend over time with optional game filter.
    /// </summary>
    private async Task<IReadOnlyList<TimeSeriesDataPoint>> GetApiRequestTrendAsync(
        DateTime fromDate,
        DateTime toDate,
        string? gameId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= fromDate && log.CreatedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(gameId) && Guid.TryParse(gameId, out var gameGuid))
        {
            query = query.Where(log => log.GameId == gameGuid);
        }

        var data = await query
            .GroupBy(log => log.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.LongCount(),
                AvgConfidence = g.Average(log => log.Confidence)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return FillMissingDates(
            data.Select(d => new TimeSeriesDataPoint(d.Date, d.Count, d.AvgConfidence)).ToList(),
            fromDate,
            toDate);
    }

    /// <summary>
    /// Get PDF upload trend over time with optional game filter.
    /// </summary>
    private async Task<IReadOnlyList<TimeSeriesDataPoint>> GetPdfUploadTrendAsync(
        DateTime fromDate,
        DateTime toDate,
        string? gameId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(pdf => pdf.UploadedAt >= fromDate && pdf.UploadedAt <= toDate);

        if (!string.IsNullOrWhiteSpace(gameId) && Guid.TryParse(gameId, out var gameGuid))
        {
            query = query.Where(pdf => pdf.GameId == gameGuid);
        }

        var data = await query
            .GroupBy(pdf => pdf.UploadedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.LongCount(),
                AvgPages = g.Average(pdf => (double?)pdf.PageCount)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return FillMissingDates(
            data.Select(d => new TimeSeriesDataPoint(d.Date, d.Count, d.AvgPages)).ToList(),
            fromDate,
            toDate);
    }

    /// <summary>
    /// Get chat message trend over time.
    /// Note: ChatLogEntity doesn't have GameId, so game filtering is not available for chat messages.
    /// </summary>
    private async Task<IReadOnlyList<TimeSeriesDataPoint>> GetChatMessageTrendAsync(
        DateTime fromDate,
        DateTime toDate,
                CancellationToken cancellationToken)
    {
        var data = await _dbContext.ChatLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= fromDate && log.CreatedAt <= toDate)
            .GroupBy(log => log.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.LongCount()
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return FillMissingDates(
            data.Select(d => new TimeSeriesDataPoint(d.Date, d.Count, null)).ToList(),
            fromDate,
            toDate);
    }

    /// <summary>
    /// Export dashboard data in CSV or JSON format.
    /// </summary>
    public async Task<string> ExportDashboardDataAsync(
        ExportDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new AnalyticsQueryParams(
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Days: 30,
            GameId: request.GameId
        );

        var stats = await GetDashboardStatsAsync(queryParams, cancellationToken).ConfigureAwait(false);

        return request.Format.ToLowerInvariant() switch
        {
            "csv" => ExportToCsv(stats),
            "json" => ExportToJson(stats),
            _ => throw new ArgumentException($"Unsupported export format: {request.Format}", nameof(request))
        };
    }

    /// <summary>
    /// Export dashboard statistics to CSV format.
    /// </summary>
    private static string ExportToCsv(DashboardStatsDto stats)
    {
        var sb = new StringBuilder();

        // Metrics section
        sb.AppendLine("Metric,Value");
#pragma warning disable MA0011 // False positive: Integer values, no culture-sensitive formatting
        sb.AppendLine($"Total Users,{stats.Metrics.TotalUsers}");
        sb.AppendLine($"Active Sessions,{stats.Metrics.ActiveSessions}");
        sb.AppendLine($"API Requests Today,{stats.Metrics.ApiRequestsToday}");
        sb.AppendLine($"Total PDF Documents,{stats.Metrics.TotalPdfDocuments}");
        sb.AppendLine($"Total Chat Messages,{stats.Metrics.TotalChatMessages}");
#pragma warning restore MA0011
        sb.AppendLine($"Average Confidence Score,{stats.Metrics.AverageConfidenceScore.ToString("F3", CultureInfo.InvariantCulture)}");
#pragma warning disable MA0011 // False positive: Integer values, no culture-sensitive formatting
        sb.AppendLine($"Total RAG Requests,{stats.Metrics.TotalRagRequests}");
        sb.AppendLine($"Total Tokens Used,{stats.Metrics.TotalTokensUsed}");
#pragma warning restore MA0011
        sb.AppendLine();

        // Time series data
        AppendTimeSeriesCsv(sb, "User Registrations", stats.UserTrend);
        AppendTimeSeriesCsv(sb, "Session Creations", stats.SessionTrend);
        AppendTimeSeriesCsv(sb, "API Requests", stats.ApiRequestTrend);
        AppendTimeSeriesCsv(sb, "PDF Uploads", stats.PdfUploadTrend);
        AppendTimeSeriesCsv(sb, "Chat Messages", stats.ChatMessageTrend);

        return sb.ToString();
    }

    private static void AppendTimeSeriesCsv(StringBuilder sb, string seriesName, IReadOnlyList<TimeSeriesDataPoint> data)
    {
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine(CultureInfo.InvariantCulture, $"{seriesName} - Date,Count,Average");
        foreach (var point in data)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)},{point.Count},{point.AverageValue?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"}");
        }
#pragma warning disable MA0011 // AppendLine() with no parameters (blank line) - no IFormatProvider overload exists
        sb.AppendLine();
#pragma warning restore MA0011
    }

    /// <summary>
    /// Export dashboard statistics to JSON format.
    /// </summary>
    private static string ExportToJson(DashboardStatsDto stats)
    {
        return JsonSerializer.Serialize(stats, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Fill missing dates in time series with zero counts for continuous visualization.
    /// Ensures charts don't have gaps for days with no data.
    /// </summary>
    private static IReadOnlyList<TimeSeriesDataPoint> FillMissingDates(
        IReadOnlyList<TimeSeriesDataPoint> data,
        DateTime fromDate,
        DateTime toDate)
    {
        var result = new List<TimeSeriesDataPoint>();
        var dataDict = data.ToDictionary(d => d.Date.Date, d => d);

        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            if (dataDict.TryGetValue(date, out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new TimeSeriesDataPoint(date, 0, null));
            }
        }

        return result;
    }
}
