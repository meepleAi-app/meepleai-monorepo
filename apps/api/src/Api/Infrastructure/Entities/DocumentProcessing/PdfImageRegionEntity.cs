namespace Api.Infrastructure.Entities;

/// <summary>
/// Image-table region (#3447): a hi_res Image/FigureCaption bbox for a PDF page, persisted so the
/// viewer can highlight table graphics. Normalized [0,1] top-left. See slice spec 2026-08-01.
/// </summary>
public class PdfImageRegionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PdfDocumentId { get; set; }
    public PdfDocumentEntity PdfDocument { get; set; } = null!;
    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string ElementType { get; set; } = "Image";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
