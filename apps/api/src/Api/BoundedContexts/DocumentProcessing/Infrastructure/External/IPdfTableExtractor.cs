using Api.Services.Pdf;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// External adapter interface for PDF table extraction
/// Abstracts the PDF library implementation (iText7) from domain logic
/// </summary>
internal interface IPdfTableExtractor
{
    /// <summary>
    /// Extracts tables from a PDF file with optional atomic rule conversion
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <param name="convertToAtomicRules">Whether to convert tables to atomic rules (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Table extraction result with tables and optional atomic rules</returns>
    Task<TableExtractionResult> ExtractTablesAsync(
        string filePath,
        bool convertToAtomicRules = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts comprehensive structured content (tables, diagrams, atomic rules) from a PDF
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured content extraction result</returns>
    Task<StructuredContentResult> ExtractStructuredContentAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of table extraction operation
/// Uses existing DTOs from Api.Services.Pdf namespace
/// </summary>
internal record TableExtractionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IList<PdfTable> Tables { get; init; } = new List<PdfTable>();
    public IList<string> AtomicRules { get; init; } = new List<string>();
    public int TableCount => Tables.Count;
    public int AtomicRuleCount => AtomicRules.Count;

    public static TableExtractionResult CreateSuccess(
        IList<PdfTable> tables,
        IList<string> atomicRules) =>
        new()
        {
            Success = true,
            Tables = tables,
            AtomicRules = atomicRules
        };

    public static TableExtractionResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Result of structured content extraction operation
/// Includes tables, diagrams, and atomic rules
/// Uses existing DTOs from Api.Services.Pdf namespace
/// </summary>
internal record StructuredContentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IList<PdfTable> Tables { get; init; } = new List<PdfTable>();
    public IList<PdfDiagram> Diagrams { get; init; } = new List<PdfDiagram>();
    public IList<string> AtomicRules { get; init; } = new List<string>();
    public int TableCount => Tables.Count;
    public int DiagramCount => Diagrams.Count;
    public int AtomicRuleCount => AtomicRules.Count;

    public static StructuredContentResult CreateSuccess(
        IList<PdfTable> tables,
        IList<PdfDiagram> diagrams,
        IList<string> atomicRules) =>
        new()
        {
            Success = true,
            Tables = tables,
            Diagrams = diagrams,
            AtomicRules = atomicRules
        };

    public static StructuredContentResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

