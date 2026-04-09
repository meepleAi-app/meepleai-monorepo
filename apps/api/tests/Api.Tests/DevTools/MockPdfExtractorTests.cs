using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

/// <summary>
/// Unit tests for <see cref="MockSmolDoclingPdfTextExtractor"/> and
/// <see cref="MockUnstructuredPdfTextExtractor"/>.
/// Verifies determinism, output shape, and correct mock content without HTTP calls.
/// </summary>
public class MockPdfExtractorTests
{
    private static readonly MockSmolDoclingPdfTextExtractor SmolDocling = new();
    private static readonly MockUnstructuredPdfTextExtractor Unstructured = new();

    // ── MockSmolDoclingPdfTextExtractor ───────────────────────────────────────

    [Fact]
    public async Task SmolDocling_ExtractTextAsync_ReturnsSuccess()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task SmolDocling_ExtractTextAsync_Returns5Pages()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(5, result.PageCount);
    }

    [Fact]
    public async Task SmolDocling_ExtractTextAsync_TextContainsMockMarker()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Contains("[MOCK smoldocling]", result.ExtractedText);
    }

    [Fact]
    public async Task SmolDocling_ExtractTextAsync_QualityIsHigh()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(ExtractionQuality.High, result.Quality);
    }

    [Fact]
    public async Task SmolDocling_ExtractTextAsync_OcrNotTriggered()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.False(result.OcrTriggered);
    }

    [Fact]
    public async Task SmolDocling_ExtractPagedTextAsync_Returns5Chunks()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(5, result.TotalPages);
        Assert.Equal(5, result.PageChunks.Count);
    }

    [Fact]
    public async Task SmolDocling_ExtractPagedTextAsync_PageNumbersAreOneIndexed()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        for (int i = 0; i < result.PageChunks.Count; i++)
        {
            Assert.Equal(i + 1, result.PageChunks[i].PageNumber);
        }
    }

    [Fact]
    public async Task SmolDocling_ExtractPagedTextAsync_EachChunkContainsMockMarker()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        Assert.All(result.PageChunks, chunk => Assert.Contains("[MOCK smoldocling]", chunk.Text));
    }

    // ── MockUnstructuredPdfTextExtractor ─────────────────────────────────────

    [Fact]
    public async Task Unstructured_ExtractTextAsync_ReturnsSuccess()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Unstructured_ExtractTextAsync_Returns3Pages()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(3, result.PageCount);
    }

    [Fact]
    public async Task Unstructured_ExtractTextAsync_TextContainsMockMarker()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Contains("[MOCK unstructured]", result.ExtractedText);
    }

    [Fact]
    public async Task Unstructured_ExtractTextAsync_QualityIsHigh()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(ExtractionQuality.High, result.Quality);
    }

    [Fact]
    public async Task Unstructured_ExtractTextAsync_OcrNotTriggered()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.False(result.OcrTriggered);
    }

    [Fact]
    public async Task Unstructured_ExtractPagedTextAsync_Returns3Chunks()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(3, result.PageChunks.Count);
    }

    [Fact]
    public async Task Unstructured_ExtractPagedTextAsync_PageNumbersAreOneIndexed()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        for (int i = 0; i < result.PageChunks.Count; i++)
        {
            Assert.Equal(i + 1, result.PageChunks[i].PageNumber);
        }
    }

    [Fact]
    public async Task Unstructured_ExtractPagedTextAsync_EachChunkContainsMockMarker()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        Assert.All(result.PageChunks, chunk => Assert.Contains("[MOCK unstructured]", chunk.Text));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SmolDocling_IsDeterministic_SameInputSameOutput()
    {
        using var stream1 = new MemoryStream(new byte[] { 1, 2, 3 });
        using var stream2 = new MemoryStream(new byte[] { 9, 8, 7 }); // different bytes — output must be identical
        var r1 = await SmolDocling.ExtractTextAsync(stream1, true, CancellationToken.None);
        var r2 = await SmolDocling.ExtractTextAsync(stream2, true, CancellationToken.None);
        Assert.Equal(r1.ExtractedText, r2.ExtractedText);
        Assert.Equal(r1.PageCount, r2.PageCount);
    }

    [Fact]
    public async Task Unstructured_IsDeterministic_SameInputSameOutput()
    {
        using var stream1 = new MemoryStream(new byte[] { 1, 2, 3 });
        using var stream2 = new MemoryStream(new byte[] { 9, 8, 7 });
        var r1 = await Unstructured.ExtractTextAsync(stream1, true, CancellationToken.None);
        var r2 = await Unstructured.ExtractTextAsync(stream2, true, CancellationToken.None);
        Assert.Equal(r1.ExtractedText, r2.ExtractedText);
        Assert.Equal(r1.PageCount, r2.PageCount);
    }

    [Fact]
    public async Task SmolDocling_CharacterCountMatchesExtractedTextLength()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(result.ExtractedText.Length, result.CharacterCount);
    }

    [Fact]
    public async Task Unstructured_CharacterCountMatchesExtractedTextLength()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractTextAsync(stream, true, CancellationToken.None);
        Assert.Equal(result.ExtractedText.Length, result.CharacterCount);
    }

    // ── CharStartIndex / CharEndIndex are non-overlapping ─────────────────────

    [Fact]
    public async Task SmolDocling_PagedChunks_CharIndicesAreMonotonicallyIncreasing()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await SmolDocling.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        for (int i = 1; i < result.PageChunks.Count; i++)
        {
            Assert.True(
                result.PageChunks[i].CharStartIndex > result.PageChunks[i - 1].CharEndIndex,
                $"Chunk {i} start ({result.PageChunks[i].CharStartIndex}) should follow chunk {i - 1} end ({result.PageChunks[i - 1].CharEndIndex})");
        }
    }

    [Fact]
    public async Task Unstructured_PagedChunks_CharIndicesAreMonotonicallyIncreasing()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await Unstructured.ExtractPagedTextAsync(stream, true, CancellationToken.None);
        for (int i = 1; i < result.PageChunks.Count; i++)
        {
            Assert.True(
                result.PageChunks[i].CharStartIndex > result.PageChunks[i - 1].CharEndIndex,
                $"Chunk {i} start ({result.PageChunks[i].CharStartIndex}) should follow chunk {i - 1} end ({result.PageChunks[i - 1].CharEndIndex})");
        }
    }
}
