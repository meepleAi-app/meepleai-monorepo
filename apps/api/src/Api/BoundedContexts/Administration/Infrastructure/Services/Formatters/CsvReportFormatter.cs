using System.Text;
using System.Globalization;

namespace Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

/// <summary>
/// CSV report formatter
/// ISSUE-916: Formats reports as CSV for data analysis
/// </summary>
internal sealed class CsvReportFormatter : IReportFormatter
{
    public byte[] Format(ReportContent content)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {content.Title}");
        sb.AppendLine($"# {content.Description}");
        sb.AppendLine($"# Generated: {content.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
        sb.AppendLine();

        // Each section as separate CSV table
        foreach (var section in content.Sections)
        {
            sb.AppendLine($"# {section.Title}");
            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                sb.AppendLine($"# {section.Description}");
            }

            if (section.Data.Count > 0)
            {
                // Header row with column names
                var columns = section.Data[0].Values.Keys.ToList();
                sb.AppendLine(string.Join(",", columns.Select(EscapeCsvValue)));

                // Data rows
                foreach (var row in section.Data)
                {
                    var values = columns.Select(col =>
                    {
                        row.Values.TryGetValue(col, out var value);
                        return EscapeCsvValue(value?.ToString() ?? string.Empty);
                    });
                    sb.AppendLine(string.Join(",", values));
                }
            }

            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public string GetFileExtension() => "csv";

    public string GetContentType() => "text/csv";

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
