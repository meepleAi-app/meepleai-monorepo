using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// ContentMetrics template implementation
/// ISSUE-916: Content and document metrics report generation
/// </summary>
internal sealed partial class ReportGeneratorService
{
    private static (bool IsValid, string? ErrorMessage) ValidateContentMetricsParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
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

    private sealed record PdfMetric(DateTime Date, int Count, long TotalSize);
    private sealed record VectorMetric(DateTime Date, int Count);
    private sealed record GameMetric(int TotalGames, int ActiveGames);

    private async Task<ReportContent> GenerateContentMetricsReportAsync(
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var startDate = (DateTime)parameters["startDate"];
        var endDate = (DateTime)parameters["endDate"];

        var pdfMetrics = await GetPdfMetricsAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
        var vectorMetrics = await GetVectorMetricsAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
        var gameMetrics = await GetGameMetricsAsync(cancellationToken).ConfigureAwait(false);

        var sections = CreateContentMetricsSections(pdfMetrics, vectorMetrics, gameMetrics);

        return new ReportContent(
            Title: "Content Metrics Report",
            Description: $"Content statistics from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["totalPdfs"] = pdfMetrics.Sum(m => m.Count),
                ["totalVectorDocs"] = vectorMetrics.Sum(v => v.Count)
            },
            Sections: sections);
    }

    private async Task<List<PdfMetric>> GetPdfMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        return await _dbContext.PdfDocuments
            .Where(p => p.UploadedAt >= startDate && p.UploadedAt <= endDate)
            .GroupBy(p => p.UploadedAt.Date)
            .Select(g => new PdfMetric(
                g.Key,
                g.Count(),
                g.Sum(p => p.FileSizeBytes)
            ))
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<List<VectorMetric>> GetVectorMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        return await _dbContext.VectorDocuments
            .Where(v => v.IndexedAt != null && v.IndexedAt >= startDate && v.IndexedAt <= endDate)
            .GroupBy(v => v.IndexedAt!.Value.Date)
            .Select(g => new VectorMetric(
                g.Key,
                g.Count()
            ))
            .OrderBy(x => x.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<GameMetric?> GetGameMetricsAsync(CancellationToken ct)
    {
        return await _dbContext.Games
            .GroupBy(g => 1)
            .Select(g => new GameMetric(
                g.Count(),
                g.Count(game => true) // Simplified, can add active criteria
            ))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private List<ReportSection> CreateContentMetricsSections(
        List<PdfMetric> pdfMetrics,
        List<VectorMetric> vectorMetrics,
        GameMetric? gameMetrics)
    {
        // ISSUE-917: Enhanced with multi-line chart
        var dateLabels = pdfMetrics.Select(m => m.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
        var pdfCountValues = pdfMetrics.Select(m => (double)m.Count).ToArray();
        var vectorCountValues = vectorMetrics.Select(v => (double)v.Count).ToArray();

        return new List<ReportSection>
        {
            new ReportSection(
                Title: "PDF Upload Metrics",
                Description: "Daily PDF upload statistics",
                Data: pdfMetrics.Select(m => new ReportDataRow(
                    new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Date"] = m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Uploads"] = m.Count,
                        ["Total Size (MB)"] = $"{m.TotalSize / 1024.0 / 1024.0:F2}"
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.Line,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>(StringComparer.Ordinal) {
                        ["Uploads"] = pdfCountValues
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "Content Processing Trends",
                Description: "PDF uploads vs Vector embeddings over time",
                Data: vectorMetrics.Select(v => new ReportDataRow(
                    new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Date"] = v.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Documents"] = v.Count
                    })).ToList(),
                Chart: new ChartData(
                    Type: ChartType.MultiLine,
                    Labels: dateLabels,
                    Series: new Dictionary<string, double[]>(StringComparer.Ordinal) {
                        ["PDF Uploads"] = pdfCountValues,
                        ["Vector Docs"] = vectorCountValues
                    },
                    YAxisLabel: "Count")),
            new ReportSection(
                Title: "Game Catalog Summary",
                Description: "Game catalog statistics",
                Data: new List<ReportDataRow>
                {
                    new(new Dictionary<string, object>(StringComparer.Ordinal) {
                        ["Metric"] = "Total Games",
                        ["Value"] = gameMetrics?.TotalGames ?? 0
                    })
                })
        };
    }
}

