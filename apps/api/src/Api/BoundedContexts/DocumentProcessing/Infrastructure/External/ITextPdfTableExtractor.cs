using iText.Kernel.Pdf;
using Api.Services.Pdf;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// iText7-based implementation of PDF table extraction
/// Infrastructure adapter that wraps the iText7 library
/// </summary>
/// <remarks>
/// This is INFRASTRUCTURE because:
/// - Depends on external library (iText7)
/// - Contains file I/O and PDF parsing logic
/// - Handles library-specific exceptions and quirks
/// - Delegates business logic to domain services
///
/// Responsibilities:
/// - PDF file reading and validation
/// - Page-by-page iteration
/// - Coordinate with table detection services
/// - Coordinate with table structure analysis services
/// - Error handling for PDF parsing failures
/// - Delegate atomic rule conversion to domain service
/// </remarks>
#pragma warning disable S101 // "IText" refers to the iText7 library name, not an interface prefix
public class ITextPdfTableExtractor : IPdfTableExtractor
#pragma warning restore S101
{
    private readonly ITableDetectionService _tableDetectionService;
    private readonly ITableStructureAnalyzer _tableStructureAnalyzer;
    private readonly TableToAtomicRuleConverter _ruleConverter;
    private readonly ILogger<ITextPdfTableExtractor> _logger;

    public ITextPdfTableExtractor(
        ITableDetectionService tableDetectionService,
        ITableStructureAnalyzer tableStructureAnalyzer,
        TableToAtomicRuleConverter ruleConverter,
        ILogger<ITextPdfTableExtractor> logger)
    {
        _tableDetectionService = tableDetectionService ?? throw new ArgumentNullException(nameof(tableDetectionService));
        _tableStructureAnalyzer = tableStructureAnalyzer ?? throw new ArgumentNullException(nameof(tableStructureAnalyzer));
        _ruleConverter = ruleConverter ?? throw new ArgumentNullException(nameof(ruleConverter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TableExtractionResult> ExtractTablesAsync(
        string filePath,
        bool convertToAtomicRules = true,
        CancellationToken ct = default)
    {
        // Validation (Infrastructure concern)
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return TableExtractionResult.CreateFailure("File path is required");
        }

        if (!File.Exists(filePath))
        {
            return TableExtractionResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            // Offload blocking I/O to thread pool
            var (tables, atomicRules) = await Task.Run(() =>
                ExtractTablesFromPdf(filePath, convertToAtomicRules), ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully extracted {TableCount} tables from PDF: {FilePath}",
                tables.Count, filePath);

            return TableExtractionResult.CreateSuccess(tables, atomicRules);
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation bubble up
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to extract tables from PDF: {FilePath}", filePath);
            return TableExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to extract tables from PDF: {FilePath}", filePath);
            return TableExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to extract tables from PDF: {FilePath}", filePath);
            return TableExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to extract tables from PDF: {FilePath}", filePath);
            return TableExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // INFRASTRUCTURE BOUNDARY PATTERN: PDF parsing can throw unexpected exceptions
            // Rationale: iText7 can throw various runtime exceptions from corrupt PDFs,
            // invalid table structures, or unexpected PDF features. We must catch all
            // exceptions at the infrastructure boundary and return error results.
            _logger.LogError(ex, "Failed to extract tables from PDF: {FilePath}", filePath);
            return TableExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <inheritdoc />
    public async Task<StructuredContentResult> ExtractStructuredContentAsync(
        string filePath,
        CancellationToken ct = default)
    {
        // Validation (Infrastructure concern)
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return StructuredContentResult.CreateFailure("File path is required");
        }

        if (!File.Exists(filePath))
        {
            return StructuredContentResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            // Offload blocking I/O to thread pool
            var result = await Task.Run(() =>
                ExtractStructuredDataFromPdf(filePath), ct).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation bubble up
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return StructuredContentResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return StructuredContentResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return StructuredContentResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return StructuredContentResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // INFRASTRUCTURE BOUNDARY PATTERN: PDF parsing can throw unexpected exceptions
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return StructuredContentResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Extracts tables from PDF using iText7 library (INFRASTRUCTURE LOGIC)
    /// </summary>
    private (List<PdfTable> Tables, List<string> AtomicRules) ExtractTablesFromPdf(
        string filePath,
        bool convertToAtomicRules)
    {
        var tables = new List<PdfTable>();
        var atomicRules = new List<string>();

        using var pdfReader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(pdfReader);

        var pageCount = pdfDoc.GetNumberOfPages();

        for (int pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = pdfDoc.GetPage(pageNum);

            // Infrastructure: Extract text lines using table detection service
            List<PositionedTextLine> pageLines;
            try
            {
                pageLines = _tableDetectionService?.ExtractPageLines(page) ?? new List<PositionedTextLine>();
            }
            catch
            {
                pageLines = new List<PositionedTextLine>();
            }

            // Infrastructure: Detect tables using table detection service
            List<PdfTable> pageTables = new();
            try
            {
                var detected = _tableDetectionService?.DetectTablesInPage(pageLines, pageNum);
                if (detected != null)
                {
                    pageTables = detected;
                }
            }
            catch
            {
                // Fallback: Use concrete detector if service fails
            }

            // Fallback to concrete detector if no tables detected
            if (pageTables.Count == 0)
            {
                var detector = new TableDetectionService(
                    new TableCellParser(),
                    NullLogger<TableDetectionService>.Instance);
                var fallbackLines = detector.ExtractPageLines(page);
                pageTables = detector.DetectTablesInPage(fallbackLines, pageNum);
            }

            // Add tables and convert to atomic rules if requested
            if (pageTables != null && pageTables.Count > 0)
            {
                tables.AddRange(pageTables);

                if (convertToAtomicRules)
                {
                    foreach (var table in pageTables)
                    {
                        // DOMAIN DELEGATION: Convert table to atomic rules using domain service
                        var rules = TableToAtomicRuleConverter.ConvertTableToAtomicRules(table);
                        if (rules != null && rules.Count > 0)
                        {
                            atomicRules.AddRange(rules);
                        }
                    }
                }
            }
        }

        _logger.LogInformation(
            "Extracted {TableCount} tables, {RuleCount} atomic rules from PDF: {FilePath}",
            tables.Count, atomicRules.Count, filePath);

        return (tables, atomicRules);
    }

    /// <summary>
    /// Extracts structured data (tables + diagrams + atomic rules) from PDF (INFRASTRUCTURE LOGIC)
    /// </summary>
    private StructuredContentResult ExtractStructuredDataFromPdf(string filePath)
    {
        var tables = new List<PdfTable>();
        var diagrams = new List<PdfDiagram>();
        var atomicRules = new List<string>();

        using var pdfReader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(pdfReader);

        var pageCount = pdfDoc.GetNumberOfPages();

        for (int pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = pdfDoc.GetPage(pageNum);

            // Infrastructure: Extract text lines
            List<PositionedTextLine> pageLines;
            try
            {
                pageLines = _tableDetectionService?.ExtractPageLines(page) ?? new List<PositionedTextLine>();
            }
            catch
            {
                pageLines = new List<PositionedTextLine>();
            }

            // Infrastructure: Detect tables
            List<PdfTable> pageTables = new();
            try
            {
                var detected = _tableDetectionService?.DetectTablesInPage(pageLines, pageNum);
                if (detected != null)
                {
                    pageTables = detected;
                }
            }
            catch
            {
                // Fallback
            }

            if (pageTables.Count == 0)
            {
                // Fallback to concrete detector
                var detector = new TableDetectionService(
                    new TableCellParser(),
                    NullLogger<TableDetectionService>.Instance);
                var fallbackLines = detector.ExtractPageLines(page);
                pageTables = detector.DetectTablesInPage(fallbackLines, pageNum);
            }

            if (pageTables != null && pageTables.Count > 0)
            {
                tables.AddRange(pageTables);

                foreach (var table in pageTables)
                {
                    // DOMAIN DELEGATION: Convert table to atomic rules
                    var rules = TableToAtomicRuleConverter.ConvertTableToAtomicRules(table);
                    if (rules != null && rules.Count > 0)
                    {
                        atomicRules.AddRange(rules);
                    }
                }
            }

            // Infrastructure: Detect diagrams
            List<PdfDiagram>? pageDiagrams = null;
            try
            {
                pageDiagrams = _tableStructureAnalyzer?.DetectDiagramsInPage(page, pageNum);
            }
            catch
            {
                // Fallback
            }

            if (pageDiagrams == null)
            {
                // Fallback to concrete analyzer
                var analyzer = new TableStructureAnalyzer(
                    NullLogger<TableStructureAnalyzer>.Instance);
                pageDiagrams = analyzer.DetectDiagramsInPage(page, pageNum);
            }

            if (pageDiagrams != null)
            {
                diagrams.AddRange(pageDiagrams);
            }
        }

        _logger.LogInformation(
            "Extracted structured content from PDF: {FilePath}, Tables: {TableCount}, Diagrams: {DiagramCount}, Atomic Rules: {RuleCount}",
            filePath, tables.Count, diagrams.Count, atomicRules.Count);

        return StructuredContentResult.CreateSuccess(tables, diagrams, atomicRules);
    }
}
