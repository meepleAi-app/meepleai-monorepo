using iText.Kernel.Pdf;
using Api.Services.Exceptions;
using Api.Services.Pdf;

namespace Api.Services;

/// <summary>
/// Coordinator service for extracting structured data (tables, diagrams) from PDF documents
/// Delegates to specialized services for detection, parsing, and analysis
/// </summary>
public class PdfTableExtractionService
{
    private readonly ITableDetectionService _tableDetectionService;
    private readonly ITableStructureAnalyzer _tableStructureAnalyzer;
    private readonly ILogger<PdfTableExtractionService> _logger;

    public PdfTableExtractionService(
        ITableDetectionService tableDetectionService,
        ITableStructureAnalyzer tableStructureAnalyzer,
        ILogger<PdfTableExtractionService> logger)
    {
        _tableDetectionService = tableDetectionService;
        _tableStructureAnalyzer = tableStructureAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Extracts tables and structured content from a PDF file
    /// </summary>
    public virtual async Task<PdfStructuredExtractionResult> ExtractStructuredContentAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PdfStructuredExtractionResult.CreateFailure("File path is required");
        }

        if (!File.Exists(filePath))
        {
            return PdfStructuredExtractionResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            var result = await Task.Run(() => ExtractStructuredData(filePath), ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation extracting structured content from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF operation: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported PDF format for structured extraction: {FilePath}", filePath);
            throw new PdfExtractionException($"Unsupported PDF format: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error extracting structured content from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to read PDF file: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument extracting structured content from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Invalid PDF argument: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting structured content from PDF: {FilePath}", filePath);
            throw new PdfExtractionException($"Failed to extract structured content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts structured data including tables and images from PDF
    /// Coordinates detection, parsing, and analysis services
    /// </summary>
    private PdfStructuredExtractionResult ExtractStructuredData(string filePath)
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

            // Delegate table detection to specialized service
            var pageLines = _tableDetectionService.ExtractPageLines(page);
            var pageTables = _tableDetectionService.DetectTablesInPage(pageLines, pageNum);
            tables.AddRange(pageTables);

            // Delegate atomic rule conversion to structure analyzer
            foreach (var table in pageTables)
            {
                var rules = _tableStructureAnalyzer.ConvertTableToAtomicRules(table);
                atomicRules.AddRange(rules);
            }

            // Delegate diagram detection to structure analyzer
            var pageDiagrams = _tableStructureAnalyzer.DetectDiagramsInPage(page, pageNum);
            diagrams.AddRange(pageDiagrams);
        }

        _logger.LogInformation(
            "Extracted structured content from PDF: {FilePath}, Tables: {TableCount}, Diagrams: {DiagramCount}, Atomic Rules: {RuleCount}",
            filePath, tables.Count, diagrams.Count, atomicRules.Count);

        return PdfStructuredExtractionResult.CreateSuccess(tables, diagrams, atomicRules);
    }
}
