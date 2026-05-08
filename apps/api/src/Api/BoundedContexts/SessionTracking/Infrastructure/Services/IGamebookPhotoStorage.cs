namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

internal interface IGamebookPhotoStorage
{
    /// <summary>
    /// Strips EXIF GPS metadata client-side (defense-in-depth) and uploads to backing blob storage.
    /// Returns the storage key (form: gamebook-photos/{campaignId}|{fileId}) consumed by Retrieve/Delete.
    /// </summary>
    Task<string> UploadAsync(Stream photoStream, string contentType, Guid campaignId, Guid photoId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the photo bytes. Caller disposes the returned stream.
    /// </summary>
    Task<Stream> RetrieveAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes from backing storage. Idempotent.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
