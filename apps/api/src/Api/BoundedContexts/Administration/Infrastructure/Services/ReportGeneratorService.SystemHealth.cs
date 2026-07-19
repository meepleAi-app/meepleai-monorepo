using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// SystemHealth template implementation
/// ISSUE-916: System health metrics report generation
/// </summary>
internal sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateSystemHealthParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Validate no unexpected parameters
        var validKeys = new HashSet<string>(StringComparer.Ordinal) { "hours", "startDate", "endDate" };
        var invalidKeys = parameters.Keys.Where(k => !validKeys.Contains(k)).ToList();
        if (invalidKeys.Count > 0)
        {
            return (false, $"Unknown parameter(s): {string.Join(", ", invalidKeys)}");
        }

        // Optional: hours parameter (default: 24)
        if (parameters.TryGetValue("hours", out var hoursObj) &&
            (hoursObj is not int hours || hours <= 0 || hours > 720))
        {
            return (false, "Parameter 'hours' must be between 1 and 720");
        }

        // Optional: startDate, endDate for alternative date range syntax
        if (parameters.TryGetValue("startDate", out var startObj) && startObj is DateTime startDate &&
            parameters.TryGetValue("endDate", out var endObj) && endObj is DateTime endDate)
        {
            if (endDate < startDate)
            {
                return (false, "Parameter 'endDate' must be after 'startDate'");
            }

            if ((endDate - startDate).TotalDays > 365)
            {
                return (false, "Date range cannot exceed 365 days");
            }
        }

        return (true, null);
    }

    private sealed record UserMetrics(int TotalUsers, int ActiveUsers, int TotalSessions, int ActiveSessions);
    private sealed record SystemContentMetrics(int TotalGames, int TotalPdfs, int RecentPdfs, int TotalChatThreads, int RecentChats);

    private async Task<ReportContent> GenerateSystemHealthReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var hours = parameters.TryGetValue("hours", out var hoursObj) && hoursObj is int h ? h : 24;
        var since = DateTime.UtcNow.AddHours(-hours);

        var userMetrics = await GetUserMetricsAsync(since, cancellationToken).ConfigureAwait(false);
        var contentMetrics = await GetSystemContentMetricsAsync(since, cancellationToken).ConfigureAwait(false);

        var sections = CreateSystemHealthSections(hours, userMetrics, contentMetrics);

        // Real health metrics from Prometheus (#3081). These were previously
        // hardcoded to 0.0 placeholders (and a mislabeled "uptime" = window param)
        // yet exported to admins as if real. When Prometheus is unavailable or has
        // no data, the key is OMITTED rather than fabricating a misleading zero.
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["hours"] = hours,
            ["since"] = since,
        };

        var (errorRate, responseTimeMs) =
            await GetSystemHealthMetricsAsync(cancellationToken).ConfigureAwait(false);
        if (errorRate.HasValue)
            metadata["errorRate"] = errorRate.Value;
        if (responseTimeMs.HasValue)
            metadata["responseTime"] = responseTimeMs.Value;

        return new ReportContent(
            Title: "System Health Report",
            Description: $"System health metrics for the last {hours} hours",
            GeneratedAt: DateTime.UtcNow,
            Metadata: metadata,
            Sections: sections);
    }

    /// <summary>
    /// Fetches the current error rate (ratio 0..1) and average response time (ms)
    /// from Prometheus, using the same queries as <c>InfrastructureDetailsService</c>.
    /// Returns null for a metric when Prometheus is unavailable or has no data, so
    /// the caller can omit it rather than reporting a fabricated 0 (#3081).
    /// </summary>
    private async Task<(double? ErrorRate, double? ResponseTimeMs)> GetSystemHealthMetricsAsync(
        CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow;
        var start = end.AddHours(-1);

        var errorRate = await QueryLatestPrometheusValueAsync(
            "sum(rate(http_requests_total{status=~\"5..\"}[1h])) / sum(rate(http_requests_total[1h]))",
            start, end, cancellationToken).ConfigureAwait(false);

        var latencySeconds = await QueryLatestPrometheusValueAsync(
            "avg(rate(http_request_duration_seconds_sum[1h]) / rate(http_request_duration_seconds_count[1h]))",
            start, end, cancellationToken).ConfigureAwait(false);

        return (errorRate, latencySeconds.HasValue ? latencySeconds.Value * 1000 : null);
    }

    /// <summary>
    /// Runs a Prometheus range query and returns the most recent data point's value,
    /// or null when there is no data or the query fails (honest degradation).
    /// </summary>
    private async Task<double?> QueryLatestPrometheusValueAsync(
        string query, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _prometheusService
                .QueryRangeAsync(query, start, end, "5m", cancellationToken)
                .ConfigureAwait(false);

            var latest = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            return latest?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Failed to query Prometheus for a system-health metric; omitting it from the report");
            return null;
        }
    }

    private async Task<UserMetrics> GetUserMetricsAsync(DateTime since, CancellationToken ct)
    {
        var totalUsers = await _dbContext.Users.CountAsync(ct).ConfigureAwait(false);
        var activeUsers = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= since)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);

        var totalSessions = await _dbContext.UserSessions.CountAsync(ct).ConfigureAwait(false);
        var activeSessions = await _dbContext.UserSessions
            .CountAsync(s => s.ExpiresAt > DateTime.UtcNow, ct)
            .ConfigureAwait(false);

        return new UserMetrics(totalUsers, activeUsers, totalSessions, activeSessions);
    }

    private async Task<SystemContentMetrics> GetSystemContentMetricsAsync(DateTime since, CancellationToken ct)
    {
        var totalGames = await _dbContext.SharedGames.CountAsync(ct).ConfigureAwait(false);
        var totalPdfs = await _dbContext.PdfDocuments.CountAsync(ct).ConfigureAwait(false);
        var recentPdfs = await _dbContext.PdfDocuments
            .CountAsync(p => p.UploadedAt >= since, ct)
            .ConfigureAwait(false);

        var totalChatThreads = await _dbContext.ChatThreads.CountAsync(ct).ConfigureAwait(false);
        var recentChats = await _dbContext.ChatThreads
            .CountAsync(c => c.CreatedAt >= since, ct)
            .ConfigureAwait(false);

        return new SystemContentMetrics(totalGames, totalPdfs, recentPdfs, totalChatThreads, recentChats);
    }

    private List<ReportSection> CreateSystemHealthSections(
        int hours,
        UserMetrics userMetrics,
        SystemContentMetrics contentMetrics)
    {
        // Create report sections with charts (ISSUE-917)
        return new List<ReportSection>
        {
            new ReportSection(
                Title: "User Metrics",
                Description: $"User statistics for the last {hours} hours",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total Users",
                        ["Value"] = userMetrics.TotalUsers
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Active Users",
                        ["Value"] = userMetrics.ActiveUsers
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total Sessions",
                        ["Value"] = userMetrics.TotalSessions
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Active Sessions",
                        ["Value"] = userMetrics.ActiveSessions
                    })
                },
                Chart: new ChartData(
                    Type: ChartType.Bar,
                    Labels: ["Total Users", "Active Users", "Total Sessions", "Active Sessions"],
                    Series: new Dictionary<string, double[]>(StringComparer.Ordinal) {
                        ["Value"] = [userMetrics.TotalUsers, userMetrics.ActiveUsers, userMetrics.TotalSessions, userMetrics.ActiveSessions]
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "Content Metrics",
                Description: "Content and document statistics",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total Games",
                        ["Value"] = contentMetrics.TotalGames
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total PDFs",
                        ["Value"] = contentMetrics.TotalPdfs
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Recent PDFs",
                        ["Value"] = contentMetrics.RecentPdfs
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total Chat Threads",
                        ["Value"] = contentMetrics.TotalChatThreads
                    }),
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Recent Chats",
                        ["Value"] = contentMetrics.RecentChats
                    })
                },
                Chart: new ChartData(
                    Type: ChartType.Bar,
                    Labels: ["Games", "Total PDFs", "Recent PDFs", "Threads", "Recent Chats"],
                    Series: new Dictionary<string, double[]>(StringComparer.Ordinal) {
                        ["Value"] = [contentMetrics.TotalGames, contentMetrics.TotalPdfs, contentMetrics.RecentPdfs, contentMetrics.TotalChatThreads, contentMetrics.RecentChats]
                    },
                    YAxisLabel: "Count"))
        };
    }
}

