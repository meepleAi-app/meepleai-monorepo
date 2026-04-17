using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Services.Pdf;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for listing vision snapshots with presigned image URLs.
/// Session Vision AI feature.
/// </summary>
internal sealed class GetVisionSnapshotsQueryHandler
    : IRequestHandler<GetVisionSnapshotsQuery, List<VisionSnapshotDto>>
{
    private readonly IVisionSnapshotRepository _snapshotRepository;
    private readonly IBlobStorageService _blobStorageService;

    public GetVisionSnapshotsQueryHandler(
        IVisionSnapshotRepository snapshotRepository,
        IBlobStorageService blobStorageService)
    {
        _snapshotRepository = snapshotRepository;
        _blobStorageService = blobStorageService;
    }

    public async Task<List<VisionSnapshotDto>> Handle(
        GetVisionSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        var snapshots = await _snapshotRepository
            .GetBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        var gameIdForStorage = request.SessionId.ToString("N");
        var result = new List<VisionSnapshotDto>(snapshots.Count);

        foreach (var snapshot in snapshots)
        {
            var imageDtos = new List<VisionSnapshotImageDto>(snapshot.Images.Count);

            foreach (var image in snapshot.Images)
            {
                var downloadUrl = await _blobStorageService
                    .GetPresignedDownloadUrlAsync(image.StorageKey, gameIdForStorage)
                    .ConfigureAwait(false);

                imageDtos.Add(new VisionSnapshotImageDto(
                    image.Id,
                    downloadUrl,
                    image.MediaType,
                    image.Width,
                    image.Height,
                    image.OrderIndex));
            }

            result.Add(new VisionSnapshotDto(
                snapshot.Id,
                snapshot.SessionId,
                snapshot.TurnNumber,
                snapshot.Caption,
                snapshot.ExtractedGameState is not null,
                snapshot.CreatedAt,
                imageDtos));
        }

        return result;
    }
}

/// <summary>
/// Handler for getting the latest extracted game state for a session.
/// Session Vision AI feature.
/// </summary>
internal sealed class GetLatestGameStateQueryHandler
    : IRequestHandler<GetLatestGameStateQuery, GameStateResult?>
{
    private readonly IVisionSnapshotRepository _snapshotRepository;

    public GetLatestGameStateQueryHandler(IVisionSnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

    public async Task<GameStateResult?> Handle(
        GetLatestGameStateQuery request,
        CancellationToken cancellationToken)
    {
        var latest = await _snapshotRepository
            .GetLatestBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is null)
            return null;

        return new GameStateResult(
            latest.Id,
            latest.ExtractedGameState,
            latest.ExtractedGameState is not null,
            latest.CreatedAt);
    }
}
