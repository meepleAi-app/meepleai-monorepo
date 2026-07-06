using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services.Pdf;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Projects stored GameNight photo entities into presigned <see cref="GameNightPhotoDto"/>s.
/// Shared by the participant-scoped and shared-token gallery query handlers (#2724).
/// </summary>
internal static class GameNightPhotoProjection
{
    public static async Task<IReadOnlyList<GameNightPhotoDto>> ResolveAsync(
        IBlobStorageService blobStorage,
        IReadOnlyList<GameNightPhotoEntity> photos,
        CancellationToken cancellationToken)
    {
        var result = new List<GameNightPhotoDto>(photos.Count);
        foreach (var p in photos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = await GameNightPhotoUrlResolver
                .ResolveAsync(blobStorage, p.BlobUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds)
                .ConfigureAwait(false);
            var thumb = p.ThumbnailUrl is null
                ? null
                : await GameNightPhotoUrlResolver
                    .ResolveAsync(blobStorage, p.ThumbnailUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds)
                    .ConfigureAwait(false);
            result.Add(new GameNightPhotoDto(
                p.Id, url, thumb, p.Caption, p.UploadedByUserId, p.UploadedAt));
        }

        return result;
    }
}
