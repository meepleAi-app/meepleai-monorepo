namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object representing report output formats
/// ISSUE-916: CSV, JSON export formats (PDF pending full implementation)
/// </summary>
public enum ReportFormat
{
    /// <summary>CSV format for data analysis</summary>
    Csv = 1,

    /// <summary>JSON format for programmatic consumption</summary>
    Json = 2

    // TODO: Add Pdf = 3 when QuestPDF integration is complete
    // Currently disabled to prevent fake PDF generation (returns markdown with .pdf extension)
}

/// <summary>
/// Extension methods for ReportFormat
/// </summary>
public static class ReportFormatExtensions
{
    public static string ToFileExtension(this ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Csv => "csv",
            ReportFormat.Json => "json",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public static string ToContentType(this ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Csv => "text/csv",
            ReportFormat.Json => "application/json",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}
