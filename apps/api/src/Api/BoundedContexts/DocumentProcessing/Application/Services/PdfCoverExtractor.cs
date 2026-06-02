using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// <see cref="IPdfCoverExtractor"/> implementation using Docnet.Core (PDFium
/// wrapper) for page rasterisation and SkiaSharp for webp encoding.
/// </summary>
/// <remarks>
/// Heuristic — multi-page cover detection (decision recorded 2026-06-02):
/// <list type="number">
/// <item>Scan the first <see cref="MaxPagesToScan"/> pages.</item>
/// <item>For each page, render at <see cref="ScanDpi"/> DPI, compute
///   <c>nonWhitePixelRatio</c> (BGRA bytes &lt; near-white threshold).</item>
/// <item>Read page text length via <see cref="IPageReader.GetText"/>.</item>
/// <item>Score = <c>nonWhitePixelRatio - (textLength / 2000.0)</c>. Penalises
///   text-heavy pages (legal disclaimers, TOC).</item>
/// <item>Pick the page with the highest score. If &lt; <see cref="MinScoreThreshold"/>
///   the PDF has no recognisable cover — return <see cref="PdfCoverExtractionOutcome.Skipped"/>.</item>
/// </list>
/// Render and webp encode happens twice — once at <see cref="ThumbnailWidth"/>×<see cref="ThumbnailHeight"/>
/// and once at <see cref="PreviewWidth"/>×<see cref="PreviewHeight"/> — so the FE
/// can serve the right size without server-side resize at read time.
/// </remarks>
internal sealed class PdfCoverExtractor : IPdfCoverExtractor
{
    public const int MaxPagesToScan = 3;
    public const int ScanDpi = 72;
    public const int ThumbnailWidth = 200;
    public const int ThumbnailHeight = 300;
    public const int PreviewWidth = 600;
    public const int PreviewHeight = 900;

    /// <summary>
    /// Threshold for marking a pixel as "non-white". BGRA bytes whose channel
    /// average exceeds this are considered background; below counts as content.
    /// Tuned so off-white scans and cream-coloured rulebook covers still count
    /// as non-background while genuinely blank pages register near-zero.
    /// </summary>
    private const byte NearWhiteThreshold = 240;

    /// <summary>
    /// Score below which we treat the PDF as having no real cover and return
    /// <see cref="PdfCoverExtractionOutcome.Skipped"/>. Heuristic: a typical
    /// cover has ratio ≥ 0.20 (20% non-white pixels); set conservatively at
    /// 0.10 to admit covers with lots of whitespace around central artwork.
    /// </summary>
    private const double MinScoreThreshold = 0.10;

    /// <summary>
    /// Quality flag for SkiaSharp webp encoding. 80 balances size (~30-50 KB
    /// per thumbnail) with visible quality. Tune as we collect post-deploy data.
    /// </summary>
    private const int WebpQuality = 80;

    private readonly ILogger<PdfCoverExtractor> _logger;

    public PdfCoverExtractor(ILogger<PdfCoverExtractor> logger)
    {
        _logger = logger;
    }

    public Task<PdfCoverExtractionResult> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return Task.FromResult(new PdfCoverExtractionResult
            {
                Outcome = PdfCoverExtractionOutcome.Failed,
                ErrorMessage = "PDF bytes are null or empty",
            });
        }

        try
        {
            var result = ExtractInternal(pdfBytes, cancellationToken);
            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF cover extraction failed");
            return Task.FromResult(new PdfCoverExtractionResult
            {
                Outcome = PdfCoverExtractionOutcome.Failed,
                ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message,
            });
        }
    }

    private PdfCoverExtractionResult ExtractInternal(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        // Render at scan DPI first to compute the heuristic — cheap rasterisation
        // for scoring. Once a page is chosen we re-render at the higher target
        // sizes for the actual artefact storage.
        var dimensions = new PageDimensions(1.0d);
        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, dimensions);
        var pageCount = docReader.GetPageCount();
        if (pageCount == 0)
        {
            return new PdfCoverExtractionResult
            {
                Outcome = PdfCoverExtractionOutcome.Skipped,
                ErrorMessage = "PDF has zero pages",
            };
        }

        var pagesToScan = Math.Min(MaxPagesToScan, pageCount);
        var bestScore = double.MinValue;
        var bestPageIndex = -1;

        for (var i = 0; i < pagesToScan; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = ScorePage(docReader, i);
            _logger.LogDebug("PDF cover scoring page {Index}: score={Score}", i, score);
            if (score > bestScore)
            {
                bestScore = score;
                bestPageIndex = i;
            }
        }

        if (bestPageIndex < 0 || bestScore < MinScoreThreshold)
        {
            _logger.LogInformation(
                "PDF cover heuristic skipped all {Scanned} candidate pages — bestScore={BestScore} threshold={Threshold}",
                pagesToScan, bestScore, MinScoreThreshold);
            return new PdfCoverExtractionResult
            {
                Outcome = PdfCoverExtractionOutcome.Skipped,
                SelectedPageIndex = bestPageIndex >= 0 ? bestPageIndex : null,
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var thumbnailBytes = RenderAndEncode(pdfBytes, bestPageIndex, ThumbnailWidth, ThumbnailHeight, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var previewBytes = RenderAndEncode(pdfBytes, bestPageIndex, PreviewWidth, PreviewHeight, cancellationToken);

        return new PdfCoverExtractionResult
        {
            Outcome = PdfCoverExtractionOutcome.Generated,
            ThumbnailWebp = thumbnailBytes,
            PreviewWebp = previewBytes,
            SelectedPageIndex = bestPageIndex,
        };
    }

    /// <summary>
    /// Renders the page once at scan DPI and computes a composite "is this a cover?"
    /// score. Higher = more likely a cover. Penalises text-heavy pages so legal
    /// disclaimers and TOC come out lower than the actual graphics-rich front cover.
    /// </summary>
    private static double ScorePage(IDocReader docReader, int pageIndex)
    {
        using var pageReader = docReader.GetPageReader(pageIndex);
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        if (width <= 0 || height <= 0)
        {
            return double.MinValue;
        }

        var rawImage = pageReader.GetImage();
        // BGRA bytes: 4 channels × width × height. nonWhitePixelRatio = pixels
        // whose RGB channel average is below NearWhiteThreshold.
        var nonWhite = 0L;
        var totalPixels = (long)width * height;
        for (var p = 0L; p + 3 < rawImage.LongLength; p += 4)
        {
            var b = rawImage[p];
            var g = rawImage[p + 1];
            var r = rawImage[p + 2];
            // ignore alpha — PDFium renders opaque pages
            var avg = (b + g + r) / 3;
            if (avg < NearWhiteThreshold)
            {
                nonWhite++;
            }
        }

        var imageRatio = totalPixels == 0 ? 0d : (double)nonWhite / totalPixels;

        var text = pageReader.GetText() ?? string.Empty;
        var textLen = text.Length;

        // Score: penalise text density by ~one unit per 2000 chars. Tuned so a
        // page with 4000 chars (typical legal page) drops below a moderately
        // image-heavy cover (ratio 0.25 → score 0.25 - 2.0 = -1.75).
        var score = imageRatio - (textLen / 2000.0d);
        return score;
    }

    /// <summary>
    /// Re-renders the chosen page at the target dimensions and webp-encodes it.
    /// Two render passes vs one-rasterise-then-resize: PDFium delivers
    /// higher-quality output when given the target size up front.
    /// </summary>
    private static byte[] RenderAndEncode(byte[] pdfBytes, int pageIndex, int width, int height, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Docnet uses fixed-dimensions render: pass target px directly.
        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(width, height));
        using var pageReader = docReader.GetPageReader(pageIndex);
        var rawBgra = pageReader.GetImage();

        // BGRA → SkiaSharp SKBitmap → webp encode.
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pixels = bitmap.GetPixels();
        System.Runtime.InteropServices.Marshal.Copy(rawBgra, 0, pixels, rawBgra.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return data.ToArray();
    }
}
