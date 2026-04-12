using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of <see cref="UnstructuredPdfTextExtractor"/>.
/// Returns 3 fixed pages with known text. No HTTP calls, no external dependencies.
/// Used by the DevTools mock-aware proxy when MOCK_UNSTRUCTURED=true.
/// </summary>
internal sealed class MockUnstructuredPdfTextExtractor : IPdfTextExtractor
{
    private static readonly IList<PageTextChunk> MockChunks = BuildMockChunks();

    private static readonly string MockFullText =
        "[MOCK unstructured] Page 1: Introduction and game overview.\n\n" +
        "[MOCK unstructured] Page 2: Core mechanics and player actions.\n\n" +
        "[MOCK unstructured] Page 3: Scoring and victory conditions.";

    /// <inheritdoc />
    public Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        var result = TextExtractionResult.CreateSuccess(
            extractedText: MockFullText,
            pageCount: 3,
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
            totalPages: 3,
            totalCharacters: MockFullText.Length,
            ocrTriggered: false);

        return Task.FromResult(result);
    }

    private static IList<PageTextChunk> BuildMockChunks()
    {
        var pages = new[]
        {
            "[MOCK unstructured] Page 1: Introduction and game overview.",
            "[MOCK unstructured] Page 2: Core mechanics and player actions.",
            "[MOCK unstructured] Page 3: Scoring and victory conditions.",
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
