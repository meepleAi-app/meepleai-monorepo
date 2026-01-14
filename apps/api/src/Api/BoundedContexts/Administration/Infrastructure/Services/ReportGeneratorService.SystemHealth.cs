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
        // Optional: hours parameter (default: 24)
        if (parameters.TryGetValue("hours", out var hoursObj) &&
            (hoursObj is not int hours || hours <= 0 || hours > 720))
        {
            return (false, "Parameter 'hours' must be between 1 and 720");
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

        return new ReportContent(
            Title: "System Health Report",
            Description: $"System health metrics for the last {hours} hours",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["hours"] = hours,
                ["since"] = since
            },
            Sections: sections);
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
        var totalGames = await _dbContext.Games.CountAsync(ct).ConfigureAwait(false);
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

