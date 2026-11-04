using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Serilog;

namespace Api.Services.Pdf;

/// <summary>
/// Conta le immagini di una pagina, con le loro dimensioni, per il rilevamento dei diagrammi.
/// </summary>
/// <remarks>
/// #4363: legge le dimensioni dal dizionario dell'immagine e non ne decodifica mai i byte. Li
/// decodificava, e li teneva in memoria senza che nessuno li leggesse. Su <c>maracaibo_rulebook.pdf</c>
/// iText andava in OutOfMemoryException un'immagine dopo l'altra, finché il runtime moriva con un
/// errore interno, e con lui il worker della coda dei PDF.
/// </remarks>
internal class ImageExtractionStrategy : IEventListener
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
                        var pdfImage = image.GetPdfObject();
                        var width = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Width)?.IntValue() ?? 0;
                        var height = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Height)?.IntValue() ?? 0;

                        _images.Add(new ExtractedImage
                        {
                            Width = width,
                            Height = height
                        });
                    }
                }
                catch (Exception ex)
                {
                    // DATA ROBUSTNESS PATTERN: Individual image extraction failures should not stop PDF processing
                    // We log at debug to preserve exception context while skipping the problematic image.
                    Serilog.Log.Debug(ex, "Image extraction skipped for one image: {Message}", ex.Message);
                }
            }
        }
    }

    public ICollection<EventType> GetSupportedEvents()
    {
        return new HashSet<EventType> { EventType.RENDER_IMAGE };
    }
}
