using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for <see cref="PdfCoverExtractor"/> — issue #1831 (umbrella #1821 L4).
/// Focused on the early-return failure modes; the heuristic + rendering paths
/// require a real PDF fixture and are covered by integration tests (Testcontainers).
/// </summary>
public class PdfCoverExtractorTests
{
    private readonly PdfCoverExtractor _sut = new(NullLogger<PdfCoverExtractor>.Instance);

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_WhenBytesAreNull()
    {
        var result = await _sut.ExtractAsync(null!, CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.ThumbnailWebp);
        Assert.Null(result.PreviewWebp);
        Assert.Null(result.SelectedPageIndex);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_WhenBytesAreEmpty()
    {
        var result = await _sut.ExtractAsync(System.Array.Empty<byte>(), CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_OnGarbageBytes()
    {
        // Random bytes that are definitely not a PDF — Docnet should throw and
        // the service should map the exception to a Failed result.
        var garbage = new byte[] { 0x42, 0x43, 0x44, 0x45, 0x00, 0xFF, 0x10, 0x20 };

        var result = await _sut.ExtractAsync(garbage, CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(result.ErrorMessage!.Length <= 500, "Error message must be truncated to ≤500 chars to fit DB column");
    }

    [Fact]
    public async Task ExtractAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var garbage = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF" magic, but truncated

        // Cancellation thrown BEFORE Docnet processes — the service either
        // throws OperationCanceledException (preferred) or returns Failed with
        // a cancellation-derived message. Both behaviours acceptable for the
        // pipeline caller; assert the operation didn't claim success.
        var result = await _sut.ExtractAsync(garbage, cts.Token).ContinueWith(t =>
            t.IsCanceled
                ? new PdfCoverExtractionResult { Outcome = PdfCoverExtractionOutcome.Failed, ErrorMessage = "cancelled" }
                : t.Result);

        Assert.NotEqual(PdfCoverExtractionOutcome.Generated, result.Outcome);
    }
}
