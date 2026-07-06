using System;
using System.Collections.Generic;
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
/// Handles the anonymous shared-gallery read (#2724). A revoked or unknown token
/// resolves to null → 404. Mirrors <see cref="GetGameNightSummaryByShareTokenQueryHandler"/>.
/// </summary>
internal sealed class GetGameNightPhotosByShareTokenQueryHandler
    : IQueryHandler<GetGameNightPhotosByShareTokenQuery, IReadOnlyList<GameNightPhotoDto>>
{
    private readonly IGameNightEventRepository _eventRepository;
    private readonly IGameNightPhotoRepository _photoRepository;
    private readonly IBlobStorageService _blobStorage;

    public GetGameNightPhotosByShareTokenQueryHandler(
        IGameNightEventRepository eventRepository,
        IGameNightPhotoRepository photoRepository,
        IBlobStorageService blobStorage)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<IReadOnlyList<GameNightPhotoDto>> Handle(
        GetGameNightPhotosByShareTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var night = await _eventRepository.GetByShareTokenAsync(query.ShareToken, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGameNight", query.ShareToken);

        var photos = await _photoRepository.GetByGameNightIdAsync(night.Id, cancellationToken).ConfigureAwait(false);
        return await GameNightPhotoProjection.ResolveAsync(_blobStorage, photos, cancellationToken).ConfigureAwait(false);
    }
}
