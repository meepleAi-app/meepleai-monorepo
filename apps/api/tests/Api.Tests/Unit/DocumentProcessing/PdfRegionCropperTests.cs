using System;
using System.Threading;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #3435 (SP4): failure-mode tests for the region cropper. The real Docnet render + SkiaSharp crop
/// path needs a PDF fixture and is covered by integration/E2E (as with <see cref="PdfCoverExtractor"/>).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class PdfRegionCropperTests
{
    private readonly PdfRegionCropper _sut = new(NullLogger<PdfRegionCropper>.Instance);

    [Fact]
    public void CropRegion_ReturnsNull_WhenBytesAreNull()
        => _sut.CropRegion(null!, 1, 0.1, 0.2, 0.5, 0.3, CancellationToken.None).Should().BeNull();

    [Fact]
    public void CropRegion_ReturnsNull_WhenBytesAreEmpty()
        => _sut.CropRegion(Array.Empty<byte>(), 1, 0.1, 0.2, 0.5, 0.3, CancellationToken.None).Should().BeNull();

    [Fact]
    public void CropRegion_ReturnsNull_OnGarbageBytes()
    {
        var garbage = new byte[] { 0x42, 0x43, 0x44, 0x45, 0x00, 0xFF, 0x10, 0x20 };
        _sut.CropRegion(garbage, 1, 0.1, 0.2, 0.5, 0.3, CancellationToken.None).Should().BeNull();
    }

    [Fact]
    public void CropRegion_Throws_WhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var pdfMagic = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var act = () => _sut.CropRegion(pdfMagic, 1, 0.1, 0.2, 0.5, 0.3, cts.Token);
        act.Should().Throw<OperationCanceledException>();
    }

    /// <summary>
    /// Issue #3571: the render DPI is a silent quality knob — the &lt;otsl&gt; gate fires either way,
    /// so a regression back to 150 DPI would not show up in any metric, only in the text of the
    /// persisted table chunk (measured: rows collapsed and characters corrupted at 150 DPI).
    /// Pin it like the region area threshold is pinned.
    /// </summary>
    [Fact]
    public void DefaultRenderScale_Is300Dpi()
    {
        PdfRegionCropper.DefaultRenderScale.Should().BeApproximately(300.0 / 72.0, 1e-9);
    }
}
