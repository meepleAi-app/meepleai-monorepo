using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Adapter that wraps EnhancedPdfProcessingOrchestrator to implement IPdfTextExtractor interface
/// Enables orchestrator to be used as drop-in replacement for single extractors
/// </summary>
/// <remarks>
/// Issue #956: P1 Bug Fix - Orchestrator feature flag was not wiring correctly
/// This adapter bridges the gap between IPdfTextExtractor (legacy interface)
/// and EnhancedPdfProcessingOrchestrator (new 3-stage implementation)
/// </remarks>
public class OrchestratedPdfTextExtractor : IPdfTextExtractor
{
    private readonly EnhancedPdfProcessingOrchestrator _orchestrator;

    public OrchestratedPdfTextExtractor(
        EnhancedPdfProcessingOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Extracts text using 3-stage orchestrator and maps result to legacy interface
    /// </summary>
    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var enhancedResult = await _orchestrator.ExtractTextWithFallbackAsync(pdfStream, enableOcrFallback, ct).ConfigureAwait(false);

        // Map EnhancedExtractionResult → TextExtractionResult
        return new TextExtractionResult(
            Success: enhancedResult.Success,
            ExtractedText: enhancedResult.ExtractedText,
            PageCount: enhancedResult.PageCount,
            CharacterCount: enhancedResult.CharacterCount,
            OcrTriggered: enhancedResult.OcrTriggered,
            Quality: enhancedResult.Quality,
            ErrorMessage: enhancedResult.ErrorMessage);
    }

    /// <summary>
    /// Extracts text page-by-page using 3-stage orchestrator
    /// </summary>
    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var enhancedResult = await _orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, enableOcrFallback, ct).ConfigureAwait(false);

        // Map EnhancedPagedExtractionResult → PagedTextExtractionResult
        return new PagedTextExtractionResult(
            Success: enhancedResult.Success,
            PageChunks: enhancedResult.PageChunks,
            TotalPages: enhancedResult.TotalPages,
            TotalCharacters: enhancedResult.TotalCharacters,
            OcrTriggered: enhancedResult.OcrTriggered,
            ErrorMessage: enhancedResult.ErrorMessage);
    }
}
