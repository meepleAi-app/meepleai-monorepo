using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for getting session media.
/// Issue #4760
/// </summary>
public class GetSessionMediaQueryHandler : IRequestHandler<GetSessionMediaQuery, IReadOnlyList<SessionMediaDto>>
{
    private readonly ISessionMediaRepository _mediaRepository;
    private readonly ISessionRepository _sessionRepository;

    public GetSessionMediaQueryHandler(ISessionMediaRepository mediaRepository, ISessionRepository sessionRepository)
    {
        _mediaRepository = mediaRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<SessionMediaDto>> Handle(GetSessionMediaQuery request, CancellationToken cancellationToken)
    {
        // #3119: session media is owner/participant-only — enforce access before returning it.
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
            throw new NotFoundException($"Session {request.SessionId} not found.");
        if (!session.IsAccessibleBy(request.RequestedBy))
            throw new ForbiddenException("You do not have access to this session.");

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
