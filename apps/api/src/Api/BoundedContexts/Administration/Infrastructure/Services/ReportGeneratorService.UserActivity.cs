using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// UserActivity template implementation
/// ISSUE-916: User activity and engagement report generation
/// </summary>
internal sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateUserActivityParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        // Required: startDate, endDate
        if (!parameters.TryGetValue("startDate", out var startObj) || startObj is not DateTime)
        {
            return (false, "Parameter 'startDate' (DateTime) is required");
        }

        if (!parameters.TryGetValue("endDate", out var endObj) || endObj is not DateTime)
        {
            return (false, "Parameter 'endDate' (DateTime) is required");
        }

        var startDate = (DateTime)startObj;
        var endDate = (DateTime)endObj;

        if (endDate < startDate)
        {
            return (false, "Parameter 'endDate' must be after 'startDate'");
        }

        return (true, null);
    }

    private async Task<ReportContent> GenerateUserActivityReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var startDate = (DateTime)parameters["startDate"];
        var endDate = (DateTime)parameters["endDate"];

        // User registration trends
        var registrations = await _dbContext.Users
            .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Login activity (via sessions)
        var logins = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Session creation
        var sessions = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // ISSUE-917: Enhanced with multi-line chart
        var dateLabels = registrations.Select(r => r.Date.ToString("MMM dd")).ToArray();
        var registrationValues = registrations.Select(r => (double)r.Count).ToArray();
        var loginValues = logins.Select(l => (double)l.Count).ToArray();

        var sections = new List<ReportSection>
        {
            new ReportSection(
                Title: "User Registrations",
                Description: "Daily new user registrations",
                Data: registrations.Select(r => new ReportDataRow(
                    new Dictionary<string, object>
(StringComparer.Ordinal) {
                        ["Date"] = r.Date.ToString("yyyy-MM-dd"),
                        ["Registrations"] = r.Count
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.Line,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>
(StringComparer.Ordinal) {
                        ["Registrations"] = registrationValues
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "User Engagement Trends",
                Description: "Daily login and session activity comparison",
                Data: logins.Select(l => new ReportDataRow(
                    new Dictionary<string, object>
(StringComparer.Ordinal) {
                        ["Date"] = l.Date.ToString("yyyy-MM-dd"),
                        ["Logins"] = l.Count
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.MultiLine,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>
(StringComparer.Ordinal) {
                        ["Logins"] = loginValues,
                        ["Sessions"] = sessions.Select(s => (double)s.Count).ToArray()
                    },
                    YAxisLabel: "Count"))
        };

        return new ReportContent(
            Title: "User Activity Report",
            Description: $"User engagement from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["totalRegistrations"] = registrations.Sum(r => r.Count),
                ["totalLogins"] = logins.Sum(l => l.Count)
            },
            Sections: sections);
    }
}
