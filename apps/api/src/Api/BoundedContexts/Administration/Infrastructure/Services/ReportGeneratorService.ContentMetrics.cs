using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// ContentMetrics template implementation
/// ISSUE-916: Content and document metrics report generation
/// </summary>
public sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateContentMetricsParameters(
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

    private async Task<ReportContent> GenerateContentMetricsReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct)
    {
        var startDate = (DateTime)parameters["startDate"];
        var endDate = (DateTime)parameters["endDate"];

        // PDF upload metrics
        var pdfMetrics = await _dbContext.PdfDocuments
            .Where(p => p.UploadedAt >= startDate && p.UploadedAt <= endDate)
            .GroupBy(p => p.UploadedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count(),
                TotalSize = g.Sum(p => p.FileSizeBytes)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Vector document metrics
        var vectorMetrics = await _dbContext.VectorDocuments
            .Where(v => v.IndexedAt != null && v.IndexedAt >= startDate && v.IndexedAt <= endDate)
            .GroupBy(v => v.IndexedAt!.Value.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Game catalog metrics
        var gameMetrics = await _dbContext.Games
            .GroupBy(g => 1)
            .Select(g => new
            {
                TotalGames = g.Count(),
                ActiveGames = g.Count(game => true) // Simplified, can add active criteria
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // ISSUE-917: Enhanced with multi-line chart
        var dateLabels = pdfMetrics.Select(m => m.Date.ToString("MMM dd")).ToArray();
        var pdfCountValues = pdfMetrics.Select(m => (double)m.Count).ToArray();
        var vectorCountValues = vectorMetrics.Select(v => (double)v.Count).ToArray();

        var sections = new List<ReportSection>
        {
            new ReportSection(
                Title: "PDF Upload Metrics",
                Description: "Daily PDF upload statistics",
                Data: pdfMetrics.Select(m => new ReportDataRow(
                    new Dictionary<string, object>
                    {
                        ["Date"] = m.Date.ToString("yyyy-MM-dd"),
                        ["Uploads"] = m.Count,
                        ["Total Size (MB)"] = $"{m.TotalSize / 1024.0 / 1024.0:F2}"
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.Line,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>
                    {
                        ["Uploads"] = pdfCountValues
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "Content Processing Trends",
                Description: "PDF uploads vs Vector embeddings over time",
                Data: vectorMetrics.Select(v => new ReportDataRow(
                    new Dictionary<string, object>
                    {
                        ["Date"] = v.Date.ToString("yyyy-MM-dd"),
                        ["Documents"] = v.Count
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.MultiLine,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>
                    {
                        ["PDF Uploads"] = pdfCountValues,
                        ["Vector Docs"] = vectorCountValues
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "Game Catalog Summary",
                Description: "Game catalog statistics",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>
                    {
                        ["Metric"] = "Total Games",
                        ["Value"] = gameMetrics?.TotalGames ?? 0
                    })
                })
        };

        return new ReportContent(
            Title: "Content Metrics Report",
            Description: $"Content statistics from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["totalPdfs"] = pdfMetrics.Sum(m => m.Count),
                ["totalVectorDocs"] = vectorMetrics.Sum(v => v.Count)
            },
            Sections: sections);
    }
}
