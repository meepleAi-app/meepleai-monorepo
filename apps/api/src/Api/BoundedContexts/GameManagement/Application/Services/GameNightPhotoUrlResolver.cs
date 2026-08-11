using System;
using System.IO;
using System.Threading.Tasks;
using Api.Services.Pdf;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Resolves a stored GameNight photo blob path to a presigned download URL.
/// Shared by the upload command handler (write path) and the gallery query
/// handlers (read path) so the FilePath→fileId/folder parsing lives in one place.
/// Issue #2724. On local storage GetPresignedDownloadUrlAsync returns null → raw path.
/// </summary>
internal static class GameNightPhotoUrlResolver
{
    /// <summary>Default presigned URL TTL (1 hour), shared by all call-sites.</summary>
    internal const int DefaultExpirySeconds = 3600;

    public static async Task<string> ResolveAsync(
        IBlobStorageService blobStorage,
        string blobPath,
        int expirySeconds)
    {
        var fileName = Path.GetFileName(blobPath);
        if (string.IsNullOrEmpty(fileName))
            return blobPath;

        var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        if (underscoreIndex <= 0)
            return blobPath;

        var fileId = fileName[..underscoreIndex];
        var directory = Path.GetDirectoryName(blobPath);
        if (string.IsNullOrEmpty(directory))
            return blobPath;

        var folder = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(folder))
            return blobPath;

        var signed = await blobStorage
            .GetPresignedDownloadUrlAsync(fileId, BlobCategory.GameNightPhoto, folder, expirySeconds)
            .ConfigureAwait(false);
        return signed ?? blobPath;
    }
}
