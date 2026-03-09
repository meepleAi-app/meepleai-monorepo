using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.QueryHandlers;

/// <summary>
/// Query handler for listing session attachments.
/// Issue #5363 - Session attachment queries.
/// </summary>
internal sealed class GetSessionAttachmentsQueryHandler
    : IQueryHandler<GetSessionAttachmentsQuery, IReadOnlyList<SessionAttachmentDto>>
{
    private readonly ISessionAttachmentRepository _repository;

    public GetSessionAttachmentsQueryHandler(ISessionAttachmentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<SessionAttachmentDto>> Handle(
        GetSessionAttachmentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<SessionAttachment> attachments;

        if (query.SnapshotIndex.HasValue)
        {
            attachments = await _repository.GetBySnapshotAsync(
                query.SessionId, query.SnapshotIndex.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            attachments = await _repository.GetBySessionIdAsync(
                query.SessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        // Apply optional filters
        var filtered = attachments.AsEnumerable();

        if (query.PlayerId.HasValue)
            filtered = filtered.Where(a => a.PlayerId == query.PlayerId.Value);

        if (query.Type.HasValue)
            filtered = filtered.Where(a => a.AttachmentType == query.Type.Value);

        return filtered.Select(MapToDto).ToList();
    }

    private static SessionAttachmentDto MapToDto(SessionAttachment a)
    {
        return new SessionAttachmentDto(
            a.Id, a.SessionId, a.PlayerId,
            a.AttachmentType, a.BlobUrl, a.ThumbnailUrl,
            a.Caption, a.ContentType, a.FileSizeBytes,
            a.SnapshotIndex, a.CreatedAt);
    }
}

/// <summary>
/// Query handler for getting a single session attachment with download URL.
/// </summary>
internal sealed class GetSessionAttachmentByIdQueryHandler
    : IQueryHandler<GetSessionAttachmentByIdQuery, SessionAttachmentDetailDto>
{
    private readonly ISessionAttachmentRepository _repository;
    private readonly ISessionAttachmentService _attachmentService;
    private readonly ILiveSessionRepository _sessionRepository;

    public GetSessionAttachmentByIdQueryHandler(
        ISessionAttachmentRepository repository,
        ISessionAttachmentService attachmentService,
        ILiveSessionRepository sessionRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<SessionAttachmentDetailDto> Handle(
        GetSessionAttachmentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var attachment = await _repository.GetByIdAsync(query.AttachmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("SessionAttachment", query.AttachmentId.ToString());

        if (attachment.SessionId != query.SessionId)
            throw new NotFoundException("SessionAttachment", query.AttachmentId.ToString());

        // Get download URL (pre-signed for S3, raw for local)
        var downloadUrl = await _attachmentService.GetDownloadUrlAsync(
            attachment.BlobUrl, cancellationToken)
            .ConfigureAwait(false);

        // Resolve player display name
        var playerName = await ResolvePlayerNameAsync(
            attachment.SessionId, attachment.PlayerId, cancellationToken)
            .ConfigureAwait(false);

        return new SessionAttachmentDetailDto(
            attachment.Id,
            attachment.SessionId,
            attachment.PlayerId,
            playerName,
            attachment.AttachmentType,
            downloadUrl,
            attachment.ThumbnailUrl,
            attachment.Caption,
            attachment.ContentType,
            attachment.FileSizeBytes,
            attachment.SnapshotIndex,
            attachment.CreatedAt);
    }

    private async Task<string> ResolvePlayerNameAsync(
        Guid sessionId, Guid playerId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
            .ConfigureAwait(false);

        if (session != null)
        {
            var player = session.Players.FirstOrDefault(p => p.Id == playerId);
            if (player != null)
                return player.DisplayName;
        }

        return "Unknown Player";
    }
}

/// <summary>
/// Query handler for listing photos linked to a specific snapshot.
/// </summary>
internal sealed class GetSnapshotPhotosQueryHandler
    : IQueryHandler<GetSnapshotPhotosQuery, IReadOnlyList<SessionAttachmentDto>>
{
    private readonly ISessionAttachmentRepository _repository;

    public GetSnapshotPhotosQueryHandler(ISessionAttachmentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<SessionAttachmentDto>> Handle(
        GetSnapshotPhotosQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var attachments = await _repository.GetBySnapshotAsync(
            query.SessionId, query.SnapshotIndex, cancellationToken)
            .ConfigureAwait(false);

        return attachments.Select(a => new SessionAttachmentDto(
            a.Id, a.SessionId, a.PlayerId,
            a.AttachmentType, a.BlobUrl, a.ThumbnailUrl,
            a.Caption, a.ContentType, a.FileSizeBytes,
            a.SnapshotIndex, a.CreatedAt)).ToList();
    }
}
