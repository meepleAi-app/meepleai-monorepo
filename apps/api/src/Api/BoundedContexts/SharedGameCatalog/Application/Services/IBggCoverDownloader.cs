namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Downloads a cover image from a remote URL (typically BGG CDN) and re-uploads
/// it to our internal blob storage. Returns the R2 key on success, null on failure
/// (caller falls back to direct remote URL).
/// </summary>
internal interface IBggCoverDownloader
{
    /// <summary>
    /// Downloads the image at <paramref name="remoteImageUrl"/> and stores it in blob
    /// storage with a key derived from <paramref name="bggId"/>.
    /// </summary>
    /// <returns>The R2 key on success; null if download or upload failed (logged).</returns>
    Task<string?> DownloadAndUploadAsync(
        int bggId,
        string remoteImageUrl,
        CancellationToken cancellationToken);
}
