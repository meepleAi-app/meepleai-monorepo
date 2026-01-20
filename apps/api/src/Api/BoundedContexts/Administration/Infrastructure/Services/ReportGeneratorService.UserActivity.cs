using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using System.Globalization;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var startDate = (DateTime)parameters["startDate"];
        var endDate = (DateTime)parameters["endDate"];

        // EF Core + PostgreSQL limitation: GroupBy with .Date property doesn't translate
        // Solution: Load data first, then group in memory

        // User registration trends
        var users = await _dbContext.Users
            .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
            .Select(u => u.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registrations = users
            .GroupBy(date => date.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();

        // Login activity (via sessions)
        var sessionLogins = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .Select(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var logins = sessionLogins
            .GroupBy(date => date.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();

        // Session creation
        var sessionData = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .Select(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessions = sessionData
            .GroupBy(date => date.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();

        // ISSUE-917: Enhanced with multi-line chart
        var dateLabels = registrations.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
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
                        ["Date"] = r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
                        ["Date"] = l.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

        var totalRegistrations = registrations.Sum(r => r.Count);
        var totalLogins = logins.Sum(l => l.Count);

        return new ReportContent(
            Title: "User Activity Report",
            Description: $"User engagement from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["totalRegistrations"] = totalRegistrations,
                ["totalLogins"] = totalLogins,
                ["activeUsers"] = totalRegistrations // Active users count (new registrations in period)
            },
            Sections: sections);
    }
}

