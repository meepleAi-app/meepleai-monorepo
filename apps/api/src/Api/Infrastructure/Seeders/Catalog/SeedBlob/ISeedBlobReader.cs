namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// Readonly accessor for the seed PDF bucket (meepleai-seeds).
/// Separate from IBlobStorageService because it uses different credentials,
/// is readonly, and points to an external source-of-truth bucket.
/// </summary>
internal interface ISeedBlobReader
{
    /// <summary>
    /// True when backed by a real seed bucket. False for NoOp fallback.
    /// PdfSeeder uses this to skip entirely instead of logging per-entry errors.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Opens a read stream to the blob at the given key.
    /// Throws if the key does not exist or the reader is a NoOp.
    /// Caller must dispose the returned stream.
    /// </summary>
    Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct);

    /// <summary>
    /// Returns true if the blob exists, false otherwise.
    /// NoOp fallback returns false without logging.
    /// </summary>
    Task<bool> ExistsAsync(string blobKey, CancellationToken ct);
}
