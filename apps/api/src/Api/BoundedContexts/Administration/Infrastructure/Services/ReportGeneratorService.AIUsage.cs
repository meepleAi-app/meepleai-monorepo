using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// AIUsage template implementation
/// ISSUE-916: AI/LLM usage and cost report generation
/// </summary>
public sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateAIUsageParameters(
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

        return (true, null);
    }

    private async Task<ReportContent> GenerateAIUsageReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var startDate = (DateTime)parameters["startDate"];
        var endDate = (DateTime)parameters["endDate"];

        // AI request metrics (using LlmCostLogs)
        var aiRequests = await _dbContext.LlmCostLogs
            .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count(),
                TotalTokens = g.Sum(r => r.TotalTokens),
                TotalCost = g.Sum(r => r.TotalCost)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Model usage breakdown
        var modelUsage = await _dbContext.LlmCostLogs
            .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
            .GroupBy(r => r.ModelId)
            .Select(g => new
            {
                Model = g.Key,
                Count = g.Count(),
                TotalCost = g.Sum(r => r.TotalCost)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // ISSUE-917: Enhanced with line chart and bar chart
        var dateLabels = aiRequests.Select(r => r.Date.ToString("MMM dd")).ToArray();
        var tokensValues = aiRequests.Select(r => (double)r.TotalTokens).ToArray();
        var costValues = aiRequests.Select(r => (double)r.TotalCost).ToArray();

        var sections = new List<ReportSection>
        {
            new ReportSection(
                Title: "Daily AI Usage Trends",
                Description: "AI request tokens and costs over time",
                Data: aiRequests.Select(r => new ReportDataRow(
                    new Dictionary<string, object>
(StringComparer.Ordinal) {
                        ["Date"] = r.Date.ToString("yyyy-MM-dd"),
                        ["Requests"] = r.Count,
                        ["Tokens"] = r.TotalTokens,
                        ["Cost (USD)"] = $"{r.TotalCost:F4}"
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.MultiLine,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>
(StringComparer.Ordinal) {
                        ["Tokens (÷1000)"] = tokensValues.Select(t => t / 1000.0).ToArray(),
                        ["Cost (USD × 100)"] = costValues.Select(c => c * 100.0).ToArray()
                    },
                    YAxisLabel: "Scaled Values")),
            new ReportSection(
                Title: "Model Usage Breakdown",
                Description: "Usage and cost by AI model",
                Data: modelUsage.Select(m => new ReportDataRow(
                    new Dictionary<string, object>
(StringComparer.Ordinal) {
                        ["Model"] = m.Model ?? "Unknown",
                        ["Requests"] = m.Count,
                        ["Total Cost (USD)"] = $"{m.TotalCost:F4}"
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.Bar,
                    Labels: modelUsage.Select(m => m.Model ?? "Unknown").ToArray(),
                    Series: new Dictionary<string, double[]>
(StringComparer.Ordinal) {
                        ["Total Cost (USD)"] = modelUsage.Select(m => (double)m.TotalCost).ToArray()
                    },
                    YAxisLabel: "Cost (USD)"))
        };

        return new ReportContent(
            Title: "AI Usage Report",
            Description: $"AI/LLM usage from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["totalRequests"] = aiRequests.Sum(r => r.Count),
                ["totalCost"] = aiRequests.Sum(r => r.TotalCost)
            },
            Sections: sections);
    }
}
