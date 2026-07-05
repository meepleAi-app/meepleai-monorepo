using System.Buffers;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public static class PdfStorageKey
{
    private static readonly SearchValues<char> PathSeparators = SearchValues.Create("/\\");

    /// <summary>
    /// Bucket key for PDF storage. Uses pdf.Id to decouple from game lifecycle.
    /// Pre-migration PDFs stored under gameId bucket must be rebucket-ed by scripts/rebucket-pdfs.*
    /// </summary>
    public static string ForPdf(Guid pdfId) => pdfId.ToString("N");

    /// <summary>
    /// Extracts the bare fileId embedded in a stored blob path of shape
    /// <c>{category}/{resourceKey}/{fileId}_{sanitizedFileName}</c> (S3 forward-slash
    /// or Windows-local backslash layout). The fileId is the segment after the last path
    /// separator and before the first underscore.
    /// </summary>
    /// <remarks>
    /// Issue #2671: <c>IBlobStorageService.StoreAsync</c> generates a RANDOM fileId
    /// (<c>Guid.NewGuid().ToString("N")</c>) that is persisted inside the returned
    /// <c>FilePath</c>. The READ path MUST recover that same fileId from the persisted
    /// <c>FilePath</c> — it can NOT be reconstructed from <see cref="ForPdf"/> (the pdfId is
    /// only the <c>resourceKey</c> folder, not the fileId). Returns <c>null</c> for
    /// null/empty input or any path that does not match the expected layout (no separator,
    /// trailing separator, no underscore in the last segment, leading underscore).
    /// </remarks>
    public static string? FileIdFromPath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        // Platform-independent separators: '/' for S3 + URL-style, '\\' on Windows local FS.
        var lastSeparator = filePath.AsSpan().LastIndexOfAny(PathSeparators);
        if (lastSeparator < 0 || lastSeparator == filePath.Length - 1)
        {
            return null;
        }

        var fileName = filePath[(lastSeparator + 1)..];
        var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        // <= 0 covers: not found (-1) AND leading underscore (= 0 ⇒ empty fileId).
        if (underscoreIndex <= 0)
        {
            return null;
        }

        return fileName[..underscoreIndex];
    }
}
