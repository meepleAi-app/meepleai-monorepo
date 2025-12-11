using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// SystemHealth template implementation
/// ISSUE-916: System health metrics report generation
/// </summary>
public sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateSystemHealthParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        // Optional: hours parameter (default: 24)
        if (parameters.TryGetValue("hours", out var hoursObj))
        {
            if (hoursObj is not int hours || hours <= 0 || hours > 720)
            {
                return (false, "Parameter 'hours' must be between 1 and 720");
            }
        }

        return (true, null);
    }

    private async Task<ReportContent> GenerateSystemHealthReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct)
    {
        var hours = parameters.TryGetValue("hours", out var hoursObj) && hoursObj is int h ? h : 24;
        var since = DateTime.UtcNow.AddHours(-hours);

        // Gather system health metrics
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

        var totalGames = await _dbContext.Games.CountAsync(ct).ConfigureAwait(false);
        var totalPdfs = await _dbContext.PdfDocuments.CountAsync(ct).ConfigureAwait(false);
        var recentPdfs = await _dbContext.PdfDocuments
            .CountAsync(p => p.UploadedAt >= since, ct)
            .ConfigureAwait(false);

        var totalChatThreads = await _dbContext.ChatThreads.CountAsync(ct).ConfigureAwait(false);
        var recentChats = await _dbContext.ChatThreads
            .CountAsync(c => c.CreatedAt >= since, ct)
            .ConfigureAwait(false);

        // Create report sections
        var sections = new List<ReportSection>
        {
            new ReportSection(
                Title: "User Metrics",
                Description: $"User statistics for the last {hours} hours",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total Users",
                        ["Value"] = totalUsers
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Active Users",
                        ["Value"] = activeUsers
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total Sessions",
                        ["Value"] = totalSessions
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Active Sessions",
                        ["Value"] = activeSessions
                    })
                }),
            new ReportSection(
                Title: "Content Metrics",
                Description: "Content and document statistics",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total Games",
                        ["Value"] = totalGames
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total PDFs",
                        ["Value"] = totalPdfs
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Recent PDFs",
                        ["Value"] = recentPdfs
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total Chat Threads",
                        ["Value"] = totalChatThreads
                    }),
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Recent Chats",
                        ["Value"] = recentChats
                    })
                })
        };

        return new ReportContent(
            Title: "System Health Report",
            Description: $"System health metrics for the last {hours} hours",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["hours"] = hours,
                ["since"] = since
            },
            Sections: sections);
    }
}
