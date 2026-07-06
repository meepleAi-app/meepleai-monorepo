using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles <see cref="GetGameNightPhotosQuery"/>: participant-scoped gallery read (#2724).
/// Only the organizer or a tagged/invited participant may view the photos.
/// </summary>
internal sealed class GetGameNightPhotosQueryHandler
    : IQueryHandler<GetGameNightPhotosQuery, IReadOnlyList<GameNightPhotoDto>>
{
    private readonly IGameNightEventRepository _eventRepository;
    private readonly IGameNightPhotoRepository _photoRepository;
    private readonly IBlobStorageService _blobStorage;

    public GetGameNightPhotosQueryHandler(
        IGameNightEventRepository eventRepository,
        IGameNightPhotoRepository photoRepository,
        IBlobStorageService blobStorage)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<IReadOnlyList<GameNightPhotoDto>> Handle(
        GetGameNightPhotosQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var night = await _eventRepository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNight", query.GameNightId.ToString());

        var isParticipant = night.OrganizerId == query.CallerUserId
            || night.Rsvps.Any(r => r.UserId == query.CallerUserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view the photos.");

        var photos = await _photoRepository.GetByGameNightIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false);
        return await GameNightPhotoProjection.ResolveAsync(_blobStorage, photos, cancellationToken).ConfigureAwait(false);
    }
}
