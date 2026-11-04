using Api.BoundedContexts.DocumentProcessing.Infrastructure.Helpers;
using Api.Services.Pdf;

#pragma warning disable MA0048 // File name must match type name - handle + acquirer belong together
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// A PDF made available as a real file on disk, for the extraction adapters that take a path rather
/// than a stream (iText tables/diagrams, Tesseract, Vision).
///
/// <para>Issue #3846: those adapters open with <c>File.Exists(path)</c> and return a failure result
/// their callers drop with <c>if (!result.Success) return;</c>. Under <c>STORAGE_PROVIDER=s3</c> the
/// persisted <c>FilePath</c> is a bucket KEY, so every one of them silently produced nothing — tables,
/// diagrams and atomic rules were never extracted, while the PDF still reached <c>Ready</c>.</para>
///
/// <para>Disposing deletes the temporary copy, if one was made; a local file that already existed is
/// left alone.</para>
/// </summary>
internal sealed class PdfLocalFile : IDisposable
{
    private readonly string? _tempPath;

    private PdfLocalFile(string path, string? tempPath)
    {
        Path = path;
        _tempPath = tempPath;
    }

    /// <summary>Path of a readable local file holding the PDF.</summary>
    public string Path { get; }

    /// <summary>
    /// Returns the PDF as a local file: the path itself when it really is one (local storage), or a
    /// temporary copy pulled from blob storage otherwise. Returns <c>null</c> when the object is not
    /// in storage either — the caller decides whether that is fatal for its stage.
    /// </summary>
    public static async Task<PdfLocalFile?> AcquireAsync(
        IBlobStorageService blobStorageService,
        Guid pdfId,
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blobStorageService);

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            return new PdfLocalFile(filePath, tempPath: null);
        }

        // Same key derivation as every other read path (#2671, #3568, #3846): FilePath is the only
        // record of where the object actually went, so neither half is rebuilt from the pdfId.
        var bucketKey = PdfStorageKey.ForPdf(pdfId);
        var resourceKey = PdfStorageKey.ResourceKeyFromPath(filePath) ?? bucketKey;
        var fileId = PdfStorageKey.FileIdFromPath(filePath) ?? bucketKey;

        var stream = await blobStorageService
            .RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, cancellationToken)
            .ConfigureAwait(false);

        if (stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            var (tempPath, tempStream) = SecureTempFileHelper.CreateSecureTempFile(".pdf");
            try
            {
                await using (tempStream.ConfigureAwait(false))
                {
                    await stream.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);
                }

                return new PdfLocalFile(tempPath, tempPath);
            }
            catch
            {
                SecureTempFileHelper.CleanupTempFile(tempPath);
                throw;
            }
        }
    }

    public void Dispose() => SecureTempFileHelper.CleanupTempFile(_tempPath);
}
#pragma warning restore MA0048
