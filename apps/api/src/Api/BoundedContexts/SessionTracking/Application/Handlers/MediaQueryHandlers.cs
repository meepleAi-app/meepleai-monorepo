using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for getting session media.
/// Issue #4760
/// </summary>
public class GetSessionMediaQueryHandler : IRequestHandler<GetSessionMediaQuery, IReadOnlyList<SessionMediaDto>>
{
    private readonly ISessionMediaRepository _mediaRepository;

    public GetSessionMediaQueryHandler(ISessionMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task<IReadOnlyList<SessionMediaDto>> Handle(GetSessionMediaQuery request, CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        return media.Select(m => new SessionMediaDto(
            m.Id,
            m.SessionId,
            m.ParticipantId,
            m.FileName,
            m.ContentType,
            m.FileSizeBytes,
            m.MediaType.ToString(),
            m.Caption,
            m.ThumbnailFileId,
            m.SnapshotId,
            m.TurnNumber,
            m.IsSharedWithSession,
            m.CreatedAt,
            m.UpdatedAt
        )).ToList();
    }
}

/// <summary>
/// Handler for getting media by snapshot.
/// </summary>
public class GetMediaBySnapshotQueryHandler : IRequestHandler<GetMediaBySnapshotQuery, IReadOnlyList<SessionMediaDto>>
{
    private readonly ISessionMediaRepository _mediaRepository;

    public GetMediaBySnapshotQueryHandler(ISessionMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task<IReadOnlyList<SessionMediaDto>> Handle(GetMediaBySnapshotQuery request, CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetBySnapshotIdAsync(request.SnapshotId, cancellationToken).ConfigureAwait(false);

        return media.Select(m => new SessionMediaDto(
            m.Id,
            m.SessionId,
            m.ParticipantId,
            m.FileName,
            m.ContentType,
            m.FileSizeBytes,
            m.MediaType.ToString(),
            m.Caption,
            m.ThumbnailFileId,
            m.SnapshotId,
            m.TurnNumber,
            m.IsSharedWithSession,
            m.CreatedAt,
            m.UpdatedAt
        )).ToList();
    }
}
