

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for extracting PDF metadata
/// </summary>
public interface IPdfMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from a PDF file
    /// </summary>
    /// <param name="filePath">Path to the PDF file</param>
    /// <returns>Extracted metadata</returns>
    Task<PdfMetadata> ExtractMetadataAsync(string filePath);
}

/// <summary>
/// PDF metadata information
/// </summary>
public class PdfMetadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int PageCount { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public string? Producer { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
}
