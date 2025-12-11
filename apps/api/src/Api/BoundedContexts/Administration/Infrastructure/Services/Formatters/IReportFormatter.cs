namespace Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

/// <summary>
/// Interface for report formatters
/// ISSUE-916: Strategy pattern for different output formats
/// </summary>
public interface IReportFormatter
{
    /// <summary>
    /// Formats report data into specific output format
    /// </summary>
    byte[] Format(ReportContent content);

    /// <summary>
    /// Gets the file extension for this format
    /// </summary>
    string GetFileExtension();

    /// <summary>
    /// Gets the content type for this format
    /// </summary>
    string GetContentType();
}

/// <summary>
/// Report content with structured data
/// </summary>
public sealed record ReportContent(
    string Title,
    string Description,
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, object> Metadata,
    IReadOnlyList<ReportSection> Sections);

/// <summary>
/// Report section with data
/// </summary>
public sealed record ReportSection(
    string Title,
    string? Description,
    IReadOnlyList<ReportDataRow> Data);

/// <summary>
/// Report data row
/// </summary>
public sealed record ReportDataRow(IReadOnlyDictionary<string, object> Values);
