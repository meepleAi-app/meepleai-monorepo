using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of <see cref="SmolDoclingPdfTextExtractor"/>.
/// Returns 5 fixed pages with known text. No HTTP calls, no external dependencies.
/// Used by the DevTools mock-aware proxy when MOCK_SMOLDOCLING=true.
/// </summary>
internal sealed class MockSmolDoclingPdfTextExtractor : IPdfTextExtractor
{
    private static readonly IList<PageTextChunk> MockChunks = BuildMockChunks();

    private static readonly string MockFullText =
        "[MOCK smoldocling] Page 1: Game rules overview.\n\n" +
        "[MOCK smoldocling] Page 2: Setup instructions.\n\n" +
        "[MOCK smoldocling] Page 3: Turn sequence and actions.\n\n" +
        "[MOCK smoldocling] Page 4: End game conditions.\n\n" +
        "[MOCK smoldocling] Page 5: Appendix and FAQ.";

    /// <inheritdoc />
    public Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        var result = TextExtractionResult.CreateSuccess(
            extractedText: MockFullText,
            pageCount: 5,
            characterCount: MockFullText.Length,
            ocrTriggered: false,
            quality: ExtractionQuality.High);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        var result = PagedTextExtractionResult.CreateSuccess(
            pageChunks: MockChunks,
            totalPages: 5,
            totalCharacters: MockFullText.Length,
            ocrTriggered: false);

        return Task.FromResult(result);
    }

    private static IList<PageTextChunk> BuildMockChunks()
    {
        var pages = new[]
        {
            "[MOCK smoldocling] Page 1: Game rules overview.",
            "[MOCK smoldocling] Page 2: Setup instructions.",
            "[MOCK smoldocling] Page 3: Turn sequence and actions.",
            "[MOCK smoldocling] Page 4: End game conditions.",
            "[MOCK smoldocling] Page 5: Appendix and FAQ.",
        };

        var chunks = new List<PageTextChunk>(pages.Length);
        int charIndex = 0;

        for (int i = 0; i < pages.Length; i++)
        {
            var text = pages[i];
            chunks.Add(new PageTextChunk(
                PageNumber: i + 1,
                Text: text,
                CharStartIndex: charIndex,
                CharEndIndex: charIndex + text.Length));
            charIndex += text.Length + 2; // +2 for "\n\n" separator
        }

        return chunks.AsReadOnly();
    }
}
