using System.Threading;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3435 (SP4): renders a specific PDF page and crops a normalized [0,1] top-left region to a PNG,
/// so the VLM crop-discriminator receives just the table graphic. Docnet.Core (PDFium) render +
/// SkiaSharp crop/encode, mirroring <see cref="PdfCoverExtractor"/>.
/// </summary>
public interface IPdfRegionCropper
{
    /// <summary>
    /// Crop the region <c>[x, y, width, height]</c> (normalized [0,1] top-left) from the 1-based
    /// <paramref name="pageNumber"/> of the PDF. Returns PNG bytes, or <c>null</c> when the page or
    /// region is out of range, the crop is degenerate, or rendering fails.
    /// </summary>
    byte[]? CropRegion(
        byte[] pdfBytes,
        int pageNumber,
        double x,
        double y,
        double width,
        double height,
        CancellationToken cancellationToken);
}
