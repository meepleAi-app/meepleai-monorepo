using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace Api.Services.Pdf;

/// <summary>
/// Custom extraction strategy for images
/// </summary>
public class ImageExtractionStrategy : IEventListener
{
    private readonly List<ExtractedImage> _images = new();

    public List<ExtractedImage> GetImages() => _images;

    public void EventOccurred(IEventData data, EventType type)
    {
        if (type == EventType.RENDER_IMAGE)
        {
            var renderInfo = data as ImageRenderInfo;
            if (renderInfo != null)
            {
                try
                {
                    var image = renderInfo.GetImage();
                    if (image != null)
                    {
                        var imageBytes = image.GetImageBytes();
                        var pdfImage = image.GetPdfObject();
                        var width = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Width)?.IntValue() ?? 0;
                        var height = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Height)?.IntValue() ?? 0;

                        _images.Add(new ExtractedImage
                        {
                            Width = width,
                            Height = height,
                            Data = imageBytes
                        });
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
                {
                    // DATA ROBUSTNESS PATTERN: Individual image extraction failures should not stop PDF processing
                    // Rationale: PDFs can contain various image formats, some corrupted or unsupported. Failing to
                    // extract one image should not prevent extracting other images or diagrams from the document.
                    // We silently skip problematic images to maximize extraction success rate.
                    // Context: iText7 can throw various exceptions for malformed/encrypted/proprietary image formats
                    // Ignore errors for individual images
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }
    }

    public ICollection<EventType> GetSupportedEvents()
    {
        return new HashSet<EventType> { EventType.RENDER_IMAGE };
    }
}
