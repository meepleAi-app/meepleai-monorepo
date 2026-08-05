using System;
using System.Runtime.InteropServices;
using System.Threading;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// <see cref="IPdfRegionCropper"/> implementation using Docnet.Core (PDFium wrapper) to rasterise
/// an arbitrary page and SkiaSharp to crop the region + encode PNG. Same rendering primitives as
/// <see cref="PdfCoverExtractor"/>; the region bbox is a fraction of the page so any render
/// resolution yields the correct crop (the bbox is DPI-independent).
/// </summary>
internal sealed class PdfRegionCropper : IPdfRegionCropper
{
    // Native-size scale factor for rendering: 300/72 ≈ 4.17 gives ~300 DPI.
    //
    // Issue #3571: this was 150 DPI, which is NOT enough for the VLM to transcribe a table crop.
    // Measured on the wingspan scorecard (page 5, the region that motivated #3565), same model and
    // same GPU, only the render DPI varying:
    //   150 DPI → rows collapsed onto one line, characters corrupted ("OMUNIT", "10PONT EGF")
    //   200 DPI → structure correct, one label per row
    //   300 DPI → structure correct AND the trailing "TOTAL" row recovered
    // The <otsl> gate fires in all three cases, so the loss is invisible in the metrics and shows up
    // only in the content of the persisted chunk. Wall-clock was ~2.5-2.9s across all three: Idefics3
    // consumes a fixed number of image tokens, so raising the DPI improves what the encoder sees
    // without materially changing inference time. The cost is transient memory — the whole page is
    // rasterised before cropping, so 300 DPI is ~4x the pixels of 150 (an A4 page is ~2480x3508).
    internal const double DefaultRenderScale = 300.0 / 72.0;

    /// <summary>
    /// Ceiling on the rasterised page, in pixels. The whole page is decoded into a managed BGRA array
    /// AND a native SKBitmap before the crop is extracted, i.e. ~8 bytes/pixel across both buffers, so
    /// the render scale is a memory multiplier: an A4 at 300 DPI (~2480x3508 ≈ 8.7 MP) costs ~70 MB
    /// transiently. Rulebooks do contain oversized fold-outs and board diagrams, and nothing else here
    /// bounds the page size — a tabloid at 300 DPI is already ~17 MP (~134 MB). Above this ceiling the
    /// scale is reduced for that page so the allocation stays bounded, trading crop resolution (which
    /// #3571 measured as the quality knob) for not risking an OOM in a container with no memory limit.
    /// 12 MP leaves A4 at full 300 DPI and only bites on genuinely oversized pages.
    /// </summary>
    internal const long MaxRenderPixels = 12_000_000;

    private readonly double _renderScale;
    private readonly ILogger<PdfRegionCropper> _logger;

    public PdfRegionCropper(ILogger<PdfRegionCropper> logger, double renderScale = DefaultRenderScale)
    {
        _logger = logger;
        _renderScale = renderScale <= 0 ? DefaultRenderScale : renderScale;
    }

    public byte[]? CropRegion(
        byte[] pdfBytes,
        int pageNumber,
        double x,
        double y,
        double width,
        double height,
        CancellationToken cancellationToken)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var effectiveScale = ResolveScaleWithinPixelBudget(pdfBytes, pageNumber);

            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(effectiveScale));
            var pageCount = docReader.GetPageCount();
            var pageIndex = pageNumber - 1; // region PageNumber is 1-based; Docnet is 0-based
            if (pageIndex < 0 || pageIndex >= pageCount)
            {
                _logger.LogWarning(
                    "PdfRegionCropper: page {Page} out of range (pageCount={Count})", pageNumber, pageCount);
                return null;
            }

            using var pageReader = docReader.GetPageReader(pageIndex);
            var renderWidth = pageReader.GetPageWidth();
            var renderHeight = pageReader.GetPageHeight();
            if (renderWidth <= 0 || renderHeight <= 0)
            {
                return null;
            }

            // Region [0,1] top-left -> pixel rect, clamped to the rendered page (no Y-flip).
            var left = (int)Math.Round(Math.Clamp(x, 0d, 1d) * renderWidth);
            var top = (int)Math.Round(Math.Clamp(y, 0d, 1d) * renderHeight);
            var right = (int)Math.Round(Math.Clamp(x + width, 0d, 1d) * renderWidth);
            var bottom = (int)Math.Round(Math.Clamp(y + height, 0d, 1d) * renderHeight);
            var cropWidth = right - left;
            var cropHeight = bottom - top;
            if (cropWidth <= 0 || cropHeight <= 0)
            {
                _logger.LogWarning(
                    "PdfRegionCropper: degenerate crop rect ({Width}x{Height}) on page {Page}",
                    cropWidth, cropHeight, pageNumber);
                return null;
            }

            var rawBgra = pageReader.GetImage();

            using var pageBitmap = new SKBitmap(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            Marshal.Copy(rawBgra, 0, pageBitmap.GetPixels(), rawBgra.Length);

            using var cropBitmap = new SKBitmap(cropWidth, cropHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            if (!pageBitmap.ExtractSubset(cropBitmap, new SKRectI(left, top, right, bottom)))
            {
                _logger.LogWarning("PdfRegionCropper: ExtractSubset failed on page {Page}", pageNumber);
                return null;
            }

            using var image = SKImage.FromBitmap(cropBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PdfRegionCropper: crop failed on page {Page}", pageNumber);
            return null;
        }
    }

    /// <summary>
    /// Returns the render scale to use for this page, reduced if rendering it at
    /// <see cref="_renderScale"/> would exceed <see cref="MaxRenderPixels"/>. Measuring costs one
    /// extra document open at scale 1.0, which parses but does NOT rasterise — the expensive call is
    /// <c>GetImage()</c>, made once on the real reader.
    /// </summary>
    private double ResolveScaleWithinPixelBudget(byte[] pdfBytes, int pageNumber)
    {
        try
        {
            using var probeReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1.0));
            var pageIndex = pageNumber - 1;
            if (pageIndex < 0 || pageIndex >= probeReader.GetPageCount())
            {
                return _renderScale; // out-of-range is reported by the caller
            }

            using var probePage = probeReader.GetPageReader(pageIndex);
            long nativeWidth = probePage.GetPageWidth();
            long nativeHeight = probePage.GetPageHeight();
            if (nativeWidth <= 0 || nativeHeight <= 0)
            {
                return _renderScale;
            }

            var projectedPixels = nativeWidth * nativeHeight * _renderScale * _renderScale;
            if (projectedPixels <= MaxRenderPixels)
            {
                return _renderScale;
            }

            var reduced = Math.Sqrt(MaxRenderPixels / (double)(nativeWidth * nativeHeight));
            _logger.LogInformation(
                "PdfRegionCropper: page {Page} is {NativeWidth}x{NativeHeight}pt; rendering at {Reduced:F2}x "
                + "instead of {Requested:F2}x to stay within the {Budget} pixel budget",
                pageNumber, nativeWidth, nativeHeight, reduced, _renderScale, MaxRenderPixels);
            return reduced;
        }
        catch (Exception ex)
        {
            // Measuring is best-effort: if the probe fails the real open will fail too and the caller
            // reports it. Never let the guard itself break a crop that would otherwise work.
            _logger.LogDebug(ex, "PdfRegionCropper: page-size probe failed on page {Page}", pageNumber);
            return _renderScale;
        }
    }
}
