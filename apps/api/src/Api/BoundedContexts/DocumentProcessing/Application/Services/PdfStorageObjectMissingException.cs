namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Thrown when the PDF object is not at the storage key recorded in <c>PdfDocument.FilePath</c> and
/// no local file exists at that path either.
///
/// <para>Issue #3846: the read paths used to fall back to <c>new FileStream(filePath)</c>
/// unconditionally. Under <c>STORAGE_PROVIDER=s3</c> the persisted <c>FilePath</c> is an object KEY
/// (<c>pdfs/{resourceKey}/{fileId}_{name}</c>), not a filesystem path, so the fallback resolved it
/// against the process working directory and reported "Could not find a part of the path
/// '/app/pdfs/…'" — an error naming a location that never existed, which sent the diagnosis after
/// the filesystem instead of the bucket. It also landed on a NULL/Unknown error category, which
/// <c>RetryFailedPdfsJob</c> treats as retriable, so retries burned on an object that is not
/// there.</para>
///
/// <para>The message names the key that was looked up. Callers map this to
/// <c>ErrorCategory.StorageObjectMissing</c>, which is deliberately NOT retriable.</para>
/// </summary>
internal sealed class PdfStorageObjectMissingException : InvalidOperationException
{
    public PdfStorageObjectMissingException(string fileId, string resourceKey, string? filePath)
        : base($"PDF object not found in storage at key 'pdf/{resourceKey}/{fileId}' " +
               $"(FilePath='{filePath}'), and no local file exists at that path.")
    {
        FileId = fileId;
        ResourceKey = resourceKey;
        FilePath = filePath;
    }

    public string FileId { get; }

    public string ResourceKey { get; }

    public string? FilePath { get; }
}
