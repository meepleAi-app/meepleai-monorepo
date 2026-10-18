using Xunit;
using FluentAssertions;
using Api.Services.Pdf;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Xobject;
using Api.Tests.Constants;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]

public class ImageExtractionStrategyTests
{
    [Fact]
    public void EventOccurred_ShouldNotThrow_WhenDataIsNull()
    {
        var strategy = new ImageExtractionStrategy();
        var ex = Record.Exception(() => strategy.EventOccurred(null!, EventType.RENDER_IMAGE));
        ex.Should().BeNull();
    }

    [Fact]
    public void GetSupportedEvents_Returns_RenderImage()
    {
        var strategy = new ImageExtractionStrategy();
        var events = strategy.GetSupportedEvents();
        events.Should().Contain(EventType.RENDER_IMAGE);
    }

    /// <summary>
    /// #4363: il rilevamento dei diagrammi vuole il numero e le dimensioni delle immagini, che stanno
    /// nel loro dizionario, non i byte. Decodificarli costava un'OutOfMemoryException dopo l'altra su
    /// <c>maracaibo_rulebook.pdf</c>, finché il runtime moriva e il bake dello snapshot restava ad
    /// aspettare una coda che nessuno drenava più. Un'immagine che iText non sa decodificare si
    /// conta lo stesso: se la strategia toccasse i byte, finirebbe scartata.
    /// </summary>
    [Fact]
    public void AnImageWhoseBytesCannotBeDecoded_IsStillCounted_WithItsSize()
    {
        using var pdf = new PdfDocument(new PdfReader(new MemoryStream(PdfWithAnUndecodableImage(width: 640, height: 480))));
        var strategy = new ImageExtractionStrategy();

        new PdfCanvasProcessor(strategy).ProcessPageContent(pdf.GetPage(1));

        var image = strategy.GetImages().Should().ContainSingle().Subject;
        image.Width.Should().Be(640);
        image.Height.Should().Be(480);
    }

    /// <summary>
    /// Una pagina con un'immagine compressa con un filtro che iText non conosce: la scrive com'è, e la
    /// lettura dei byte decodificati fallisce sempre. (Con FlateDecode su byte non validi no: il
    /// decoder di iText è tollerante e restituisce quel che riesce.)
    /// </summary>
    private static byte[] PdfWithAnUndecodableImage(int width, int height)
    {
        using var output = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfWriter(output)))
        {
            var stream = new PdfStream(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
            stream.Put(PdfName.Type, PdfName.XObject);
            stream.Put(PdfName.Subtype, PdfName.Image);
            stream.Put(PdfName.Width, new PdfNumber(width));
            stream.Put(PdfName.Height, new PdfNumber(height));
            stream.Put(PdfName.ColorSpace, PdfName.DeviceRGB);
            stream.Put(PdfName.BitsPerComponent, new PdfNumber(8));
            stream.Put(PdfName.Filter, new PdfName("MeepleUnknownDecode"));

            var page = pdf.AddNewPage();
            new PdfCanvas(page).AddXObjectFittedIntoRectangle(new PdfImageXObject(stream), new iText.Kernel.Geom.Rectangle(0, 0, width / 4f, height / 4f));
        }

        return output.ToArray();
    }
}
