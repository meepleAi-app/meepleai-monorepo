namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;

/// <summary>
/// #4449: one size limit for PDFs, everywhere — <see cref="PdfProcessingOptions.MaxFileSizeBytes"/>
/// (<c>PdfProcessing:MaxFileSizeBytes</c>). Every upload validator, handler and endpoint reads it from
/// there; this class holds only what they share around it.
/// </summary>
/// <remarks>
/// Before #4449 each layer had its own number (50, 100, 150, 200, 500 MB), and the upload layers were
/// wider than processing: a PDF between 100 and 200 MB was accepted, stored, and then failed in
/// extraction. A plan's PDF size (<c>TierLimits.MaxPdfSizeBytes</c>) is a commercial cap on top of
/// this one, not a second technical limit: what a user may upload is the smaller of the two.
/// </remarks>
internal static class PdfSizeLimit
{
    /// <summary>
    /// The highest value the configuration may set: the extraction service's own ceiling
    /// (<c>max_file_size</c> in <c>apps/unstructured-service/src/config/settings.py</c>). Above it a
    /// PDF would be accepted at upload and then refused by the extractor.
    /// </summary>
    public const long ExtractionServiceMaxBytes = 104_857_600;

    /// <summary>
    /// Room for the multipart envelope around the file: boundaries, part headers and the other form
    /// fields. The request limit is the file limit plus this, so a file just over the limit reaches
    /// the validator and is refused with a 400 that names the limit — not with a bare 413 from
    /// Kestrel — and a file exactly at the limit is not refused because of its envelope.
    /// </summary>
    public const long MultipartOverheadBytes = 1024 * 1024;

    /// <summary>The request body limit of an endpoint that receives one PDF.</summary>
    public static long RequestBodyLimit(long maxFileSizeBytes) => maxFileSizeBytes + MultipartOverheadBytes;

    /// <summary>The limit as messages show it, in whole megabytes (MiB): 104 857 600 → "100 MB".</summary>
    public static string Describe(long bytes) => $"{bytes / (1024 * 1024)} MB";
}
