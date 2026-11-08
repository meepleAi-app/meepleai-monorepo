using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for uploading media to a session.
/// Issue #4760 - SessionMedia Entity
/// </summary>
public class UploadSessionMediaCommandHandler : IRequestHandler<UploadSessionMediaCommand, UploadSessionMediaResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionMediaRepository _mediaRepository;
    private readonly IMediator _mediator;
    private readonly ISessionAccessGuard _accessGuard;

    public UploadSessionMediaCommandHandler(
        ISessionAccessGuard accessGuard,
        ISessionRepository sessionRepository,
        ISessionMediaRepository mediaRepository,
        IMediator mediator)
    {
        _accessGuard = accessGuard ?? throw new ArgumentNullException(nameof(accessGuard));
        _sessionRepository = sessionRepository;
        _mediaRepository = mediaRepository;
        _mediator = mediator;
    }

    public async Task<UploadSessionMediaResult> Handle(UploadSessionMediaCommand request, CancellationToken cancellationToken)
    {
        // IDOR guard (#3756): prima di qualunque mutazione, scrittura o broadcast SSE.
        await _accessGuard.EnsureOwnerOrParticipantAsync(
            request.SessionId, request.RequestedBy, cancellationToken).ConfigureAwait(false);

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        var mediaType = Enum.Parse<SessionMediaType>(request.MediaType);

        var media = SessionMedia.Create(
            request.SessionId,
            request.ParticipantId,
            request.FileId,
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            mediaType,
            request.Caption,
            request.SnapshotId,
            request.TurnNumber);

        await _mediaRepository.AddAsync(media, cancellationToken).ConfigureAwait(false);
        await _mediaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionMediaUploadedEvent
        {
            SessionId = request.SessionId,
            MediaId = media.Id,
            ParticipantId = request.ParticipantId,
            MediaType = mediaType,
            FileName = request.FileName,
        }, cancellationToken).ConfigureAwait(false);

        return new UploadSessionMediaResult(media.Id);
    }
}

/// <summary>
/// Handler for updating media caption.
/// </summary>
public class UpdateMediaCaptionCommandHandler : IRequestHandler<UpdateMediaCaptionCommand, Unit>
{
    private readonly ISessionMediaRepository _mediaRepository;
    private readonly ISessionRepository _sessionRepository;

    public UpdateMediaCaptionCommandHandler(
        ISessionMediaRepository mediaRepository,
        ISessionRepository sessionRepository)
    {
        _mediaRepository = mediaRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<Unit> Handle(UpdateMediaCaptionCommand request, CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetByIdAsync(request.MediaId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Media {request.MediaId} not found");

        // #2655 IDOR guard: resolve the owning participant server-side from the
        // authenticated caller — never trust a client-supplied participant id.
        var session = await _sessionRepository.GetByIdAsync(media.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {media.SessionId} not found");
        var owner = session.Participants.FirstOrDefault(p => p.Id == media.ParticipantId);
        if (owner is null || owner.UserId != request.RequesterUserId)
            throw new ForbiddenException("Only the media owner can update the caption.");

        media.UpdateCaption(request.Caption);
        await _mediaRepository.UpdateAsync(media, cancellationToken).ConfigureAwait(false);
        await _mediaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

/// <summary>
/// Handler for deleting media from a session.
/// </summary>
public class DeleteSessionMediaCommandHandler : IRequestHandler<DeleteSessionMediaCommand, Unit>
{
    private readonly ISessionMediaRepository _mediaRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;

    public DeleteSessionMediaCommandHandler(
        ISessionMediaRepository mediaRepository,
        ISessionRepository sessionRepository,
        IMediator mediator)
    {
        _mediaRepository = mediaRepository;
        _sessionRepository = sessionRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(DeleteSessionMediaCommand request, CancellationToken cancellationToken)
    {
        var media = await _mediaRepository.GetByIdAsync(request.MediaId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Media {request.MediaId} not found");

        // #2655 IDOR guard: resolve the owning participant server-side from the
        // authenticated caller — never trust a client-supplied participant id.
        // Only the user who owns the uploading participant may delete the media.
        var session = await _sessionRepository.GetByIdAsync(media.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {media.SessionId} not found");
        var owner = session.Participants.FirstOrDefault(p => p.Id == media.ParticipantId);
        if (owner is null || owner.UserId != request.RequesterUserId)
            throw new ForbiddenException("Only the media owner can delete it.");

        media.SoftDelete();
        await _mediaRepository.UpdateAsync(media, cancellationToken).ConfigureAwait(false);
        await _mediaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionMediaDeletedEvent
        {
            SessionId = media.SessionId,
            MediaId = media.Id,
            ParticipantId = media.ParticipantId,
        }, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
