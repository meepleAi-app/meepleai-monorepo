namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object representing report output formats
/// ISSUE-916: CSV, JSON export formats (PDF pending full implementation)
/// </summary>
internal enum ReportFormat
{
    /// <summary>CSV format for data analysis</summary>
    Csv = 1,

    /// <summary>JSON format for programmatic consumption</summary>
    Json = 2,

    /// <summary>PDF format with charts and professional layout</summary>
    /// <remarks>ISSUE-917: QuestPDF integration with ScottPlot charts</remarks>
    Pdf = 3
}

/// <summary>
/// Extension methods for ReportFormat
/// </summary>
internal static class ReportFormatExtensions
{
    public static string ToFileExtension(this ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Csv => "csv",
            ReportFormat.Json => "json",
            ReportFormat.Pdf => "pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public static string ToContentType(this ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Csv => "text/csv",
            ReportFormat.Json => "application/json",
            ReportFormat.Pdf => "application/pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}
