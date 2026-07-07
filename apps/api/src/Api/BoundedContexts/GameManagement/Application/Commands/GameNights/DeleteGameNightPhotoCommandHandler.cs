using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles removing a photo from a GameNight recap gallery (#2724). Allowed for the
/// uploader or the night's organizer. Deletes the DB row then best-effort deletes the blob.
/// </summary>
internal sealed class DeleteGameNightPhotoCommandHandler : ICommandHandler<DeleteGameNightPhotoCommand>
{
    private readonly IGameNightEventRepository _eventRepository;
    private readonly IGameNightPhotoRepository _photoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<DeleteGameNightPhotoCommandHandler> _logger;

    public DeleteGameNightPhotoCommandHandler(
        IGameNightEventRepository eventRepository,
        IGameNightPhotoRepository photoRepository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorage,
        ILogger<DeleteGameNightPhotoCommandHandler> logger)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeleteGameNightPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var photo = await _photoRepository.GetByIdAsync(command.PhotoId, cancellationToken).ConfigureAwait(false);
        if (photo is null || photo.GameNightId != command.GameNightId)
            throw new NotFoundException("GameNightPhoto", command.PhotoId.ToString());

        var night = await _eventRepository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNight", command.GameNightId.ToString());

        var canDelete = photo.UploadedByUserId == command.UserId || night.OrganizerId == command.UserId;
        if (!canDelete)
            throw new ForbiddenException("Only the uploader or the organizer can delete this photo.");

        await _photoRepository.RemoveAsync(photo, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Best-effort blob cleanup (orphan is harmless if this fails).
        await TryDeleteBlobAsync(photo.BlobUrl, cancellationToken).ConfigureAwait(false);
        if (photo.ThumbnailUrl is not null)
            await TryDeleteBlobAsync(photo.ThumbnailUrl, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryDeleteBlobAsync(string blobPath, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(blobPath);
            var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
            if (underscoreIndex <= 0) return;
            var fileId = fileName[..underscoreIndex];
            var folder = Path.GetFileName(Path.GetDirectoryName(blobPath) ?? string.Empty);
            if (string.IsNullOrEmpty(folder)) return;
            await _blobStorage.DeleteAsync(fileId, BlobCategory.GameNightPhoto, folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Best-effort blob delete failed for {BlobPath}", blobPath);
        }
    }
}
