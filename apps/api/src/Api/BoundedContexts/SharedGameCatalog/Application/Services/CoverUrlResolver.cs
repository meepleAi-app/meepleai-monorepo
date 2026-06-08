using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services.Pdf;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1852 (Gap A) + Gap G2 (2026-06-08): centralizes cover-URL resolution
/// with the priority L3 (user custom) -> L4 (PDF-derived) -> L2.5 (BGG re-uploaded)
/// -> L2 (Wikidata) -> null. Each layer falls through to the next when its R2 key
/// is missing or the blob storage cannot mint a presigned URL (returns null in dev / local).
/// </summary>
internal static class CoverUrlResolver
{
    public static async Task<string?> ResolveForUserAsync(
        SharedGameEntity sharedGame,
        UserLibraryEntryEntity? userEntry,
        IBlobStorageService blobStorage)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        ArgumentNullException.ThrowIfNull(blobStorage);

        if (!string.IsNullOrWhiteSpace(userEntry?.CustomCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedDownloadUrlAsync(
                    $"{userEntry.CustomCoverR2Key}.webp",
                    BlobCategory.GameImage,
                    userEntry.CustomCoverR2Key)
                .ConfigureAwait(false);
            if (url is not null) return url;
        }

        return await ResolvePublicAsync(sharedGame, blobStorage).ConfigureAwait(false);
    }

    public static async Task<string?> ResolvePublicAsync(
        SharedGameEntity sharedGame,
        IBlobStorageService blobStorage)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        ArgumentNullException.ThrowIfNull(blobStorage);

        if (!string.IsNullOrWhiteSpace(sharedGame.PdfCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedDownloadUrlAsync(
                    $"{sharedGame.PdfCoverR2Key}-preview.webp",
                    BlobCategory.GameImage,
                    sharedGame.PdfCoverR2Key)
                .ConfigureAwait(false);
            if (url is not null) return url;
        }

        // L2.5 BGG re-uploaded cover (Gap G2)
        // Asymmetry vs L4/L2: BGG cover is stored as a single asset under the raw
        // resource key (set by BggCoverDownloader.DownloadAndUploadAsync), with no
        // -preview.webp or .webp suffix. The blob service treats arg 1 as the literal
        // storage object path; arg 3 is the cache identifier (same key here is fine
        // because the storage object IS the cache target).
        if (!string.IsNullOrWhiteSpace(sharedGame.BggCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedDownloadUrlAsync(
                    sharedGame.BggCoverR2Key,
                    BlobCategory.GameImage,
                    sharedGame.BggCoverR2Key)
                .ConfigureAwait(false);
            if (url is not null) return url;
        }

        // L2 Wikidata cover
        if (!string.IsNullOrWhiteSpace(sharedGame.WikidataCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedDownloadUrlAsync(
                    $"{sharedGame.WikidataCoverR2Key}.webp",
                    BlobCategory.GameImage,
                    sharedGame.WikidataCoverR2Key)
                .ConfigureAwait(false);
            if (url is not null) return url;
        }

        return null;
    }
}
