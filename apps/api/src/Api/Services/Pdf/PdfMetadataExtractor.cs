using iText.Kernel.Pdf;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for extracting PDF metadata
/// </summary>
public class PdfMetadataExtractor : IPdfMetadataExtractor
{
    private readonly ILogger<PdfMetadataExtractor> _logger;

    public PdfMetadataExtractor(ILogger<PdfMetadataExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<PdfMetadata> ExtractMetadataAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            using var pdfReader = new PdfReader(filePath);
            using var pdfDoc = new PdfDocument(pdfReader);

            var info = pdfDoc.GetDocumentInfo();

            var metadata = new PdfMetadata
            {
                PageCount = pdfDoc.GetNumberOfPages(),
                Title = info.GetTitle(),
                Author = info.GetAuthor(),
                Subject = info.GetSubject(),
                Keywords = info.GetKeywords(),
                Producer = info.GetProducer()
            };

            // Parse creation date if available
            var creationDateStr = info.GetMoreInfo(PdfName.CreationDate.GetValue());
            if (!string.IsNullOrEmpty(creationDateStr))
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    metadata.CreationDate = ParsePdfDate(creationDateStr);
                }
                catch (Exception ex)
                {
                    // DATA ROBUSTNESS PATTERN: Malformed PDF metadata should not fail extraction
                    // Rationale: PDF creation dates can have various non-standard formats. Parsing failures
                    // should only result in null date metadata, not extraction failure. We log the error
                    // for monitoring but continue extracting other metadata fields.
                    // Context: PDF date format D:YYYYMMDDHHmmSS can have vendor-specific variations
                    _logger.LogWarning(ex, "Failed to parse creation date: {Date}", creationDateStr);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            // Parse modification date if available
            var modDateStr = info.GetMoreInfo(PdfName.ModDate.GetValue());
            if (!string.IsNullOrEmpty(modDateStr))
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    metadata.ModificationDate = ParsePdfDate(modDateStr);
                }
                catch (Exception ex)
                {
                    // DATA ROBUSTNESS PATTERN: Malformed PDF metadata should not fail extraction
                    // Rationale: PDF modification dates can have various non-standard formats. Parsing failures
                    // should only result in null date metadata, not extraction failure. We log the error
                    // for monitoring but continue extracting other metadata fields.
                    // Context: PDF date format D:YYYYMMDDHHmmSS can have vendor-specific variations
                    _logger.LogWarning(ex, "Failed to parse modification date: {Date}", modDateStr);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            return metadata;
        });
    }

    private DateTime? ParsePdfDate(string pdfDate)
    {
        // PDF date format: D:YYYYMMDDHHmmSSOHH'mm'
        // Example: D:20231025120000+02'00'
        if (string.IsNullOrEmpty(pdfDate) || !pdfDate.StartsWith("D:"))
        {
            return null;
        }

        var dateStr = pdfDate.Substring(2);

        // Extract basic components (at minimum we need YYYYMMDD)
        if (dateStr.Length < 8)
        {
            return null;
        }

        var year = int.Parse(dateStr.Substring(0, 4));
        var month = int.Parse(dateStr.Substring(4, 2));
        var day = int.Parse(dateStr.Substring(6, 2));

        var hour = dateStr.Length >= 10 ? int.Parse(dateStr.Substring(8, 2)) : 0;
        var minute = dateStr.Length >= 12 ? int.Parse(dateStr.Substring(10, 2)) : 0;
        var second = dateStr.Length >= 14 ? int.Parse(dateStr.Substring(12, 2)) : 0;

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
