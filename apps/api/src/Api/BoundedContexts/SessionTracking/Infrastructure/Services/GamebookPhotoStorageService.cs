using Api.Services.Pdf;
using ImageMagick;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

internal sealed class GamebookPhotoStorageService : IGamebookPhotoStorage
{
    private const string GameIdPrefix = "gamebook-photos";
    private readonly IBlobStorageService _blob;

    public GamebookPhotoStorageService(IBlobStorageService blob)
    {
        _blob = blob ?? throw new ArgumentNullException(nameof(blob));
    }

    public async Task<string> UploadAsync(Stream photoStream, string contentType, Guid campaignId, Guid photoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photoStream);
        if (campaignId == Guid.Empty) throw new ArgumentException("campaignId required", nameof(campaignId));
        if (photoId == Guid.Empty) throw new ArgumentException("photoId required", nameof(photoId));

        var stripped = await StripExifAsync(photoStream, contentType, cancellationToken).ConfigureAwait(false);
        await using (stripped.ConfigureAwait(false))
        {
            var fileName = $"{photoId:N}.jpg";
            var gameId = $"{GameIdPrefix}/{campaignId:N}";
            var result = await _blob.StoreAsync(stripped, fileName, BlobCategory.GamebookPhoto, gameId, cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.FileId is null)
                throw new InvalidOperationException($"Photo upload failed: {result.ErrorMessage}");

            // Compose the storage key shape `gameId|fileId` so Retrieve/Delete can split it back.
            return $"{gameId}|{result.FileId}";
        }
    }

    public async Task<Stream> RetrieveAsync(string storageKey, CancellationToken cancellationToken)
    {
        var (gameId, fileId) = SplitKey(storageKey);
        var stream = await _blob.RetrieveAsync(fileId, BlobCategory.GamebookPhoto, gameId, cancellationToken).ConfigureAwait(false);
        return stream ?? throw new InvalidOperationException($"Photo not found at key '{storageKey}'");
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var (gameId, fileId) = SplitKey(storageKey);
        _ = await _blob.DeleteAsync(fileId, BlobCategory.GamebookPhoto, gameId, cancellationToken).ConfigureAwait(false);
    }

    private static (string GameId, string FileId) SplitKey(string storageKey)
    {
        var parts = storageKey.Split('|', 2);
        if (parts.Length != 2) throw new ArgumentException($"Malformed storage key '{storageKey}'", nameof(storageKey));
        return (parts[0], parts[1]);
    }

    /// <summary>
    /// Re-encodes the photo dropping EXIF metadata. Defense-in-depth — clients SHOULD strip first.
    /// ADR DEC-3d-1 (issue #2055 Phase G): Magick.NET 14.x replaces ImageSharp.
    /// <see cref="MagickImage.Strip"/> removes EXIF, IPTC, ICC, XMP, and 8BIM
    /// profiles in a single call — equivalent to clearing each
    /// <c>image.Metadata.*Profile</c> on the prior ImageSharp API.
    /// </summary>
    private static async Task<Stream> StripExifAsync(Stream input, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        // MagickImage constructor is synchronous; wrap in Task.Run to keep the
        // public async signature intact.
        using var image = await Task.Run(() => new MagickImage(input), cancellationToken).ConfigureAwait(false);
        image.Strip();

        image.Format = MagickFormat.Jpeg;
        image.Quality = 85;

        var output = new MemoryStream();
        await Task.Run(() => image.Write(output), cancellationToken).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }
}
