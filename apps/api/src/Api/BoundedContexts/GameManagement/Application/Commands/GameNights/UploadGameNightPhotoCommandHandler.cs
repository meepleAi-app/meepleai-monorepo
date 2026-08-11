using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Helpers;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles uploading a photo to a GameNight recap gallery. Flow: participant-guard →
/// buffer → magic-byte validate → SHA256 dedup → StoreAsync → thumbnail (best-effort) →
/// OCR (opt-in, best-effort) → persist standalone entity → SaveChanges → presign.
/// Issue #2724 (mirrors PlayRecord photos, ADR-067).
/// </summary>
internal sealed class UploadGameNightPhotoCommandHandler
    : ICommandHandler<UploadGameNightPhotoCommand, GameNightPhotoUploadResult>
{
    private readonly IGameNightEventRepository _eventRepository;
    private readonly IGameNightPhotoRepository _photoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorage;
    private readonly IPhotoPreprocessor _photoPreprocessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UploadGameNightPhotoCommandHandler> _logger;

    public UploadGameNightPhotoCommandHandler(
        IGameNightEventRepository eventRepository,
        IGameNightPhotoRepository photoRepository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorage,
        IPhotoPreprocessor photoPreprocessor,
        TimeProvider timeProvider,
        ILogger<UploadGameNightPhotoCommandHandler> logger)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _photoPreprocessor = photoPreprocessor ?? throw new ArgumentNullException(nameof(photoPreprocessor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameNightPhotoUploadResult> Handle(UploadGameNightPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var night = await _eventRepository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNight", command.GameNightId.ToString());

        // Participant guard (IDOR): only the organizer or a tagged/invited/accepted
        // participant may contribute to a night's recap gallery.
        var isParticipant = night.OrganizerId == command.UserId
            || night.Rsvps.Any(r => r.UserId == command.UserId);
        if (!isParticipant)
            throw new ForbiddenException("You do not have permission to add photos to this game night.");

        // Buffer the upload (needed for magic-byte, hash, thumbnail, OCR + re-reads).
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await command.FileStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        using (var validateStream = new MemoryStream(bytes, writable: false))
        {
            var ok = await ImageFileValidator
                .ValidateMagicBytesAsync(validateStream, command.MimeType).ConfigureAwait(false);
            if (!ok)
                throw new ValidationException("File content does not match its declared type.");
        }

        var sha = CryptographyHelper.ComputeSha256Hash(bytes);

        // Idempotent re-upload: same (night, sha) → return existing.
        var existing = await _photoRepository.GetBySha256Async(command.GameNightId, sha, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            var existingUrl = await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, existing.BlobUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
            var existingThumb = existing.ThumbnailUrl is null
                ? null
                : await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, existing.ThumbnailUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
            return new GameNightPhotoUploadResult(existing.Id, existingUrl, existingThumb, existing.OcrText, WasDeduplicated: true);
        }

        var photoId = Guid.NewGuid();
        var ext = string.Equals(command.MimeType, "image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                : string.Equals(command.MimeType, "image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";

        // Use underscore separator so the URL resolver can extract fileId (substring before first '_').
        var resourceKey = $"game-night-{night.Id:N}";
        var fileName = $"{photoId:N}_{sha[..8]}{ext}";

        BlobStorageResult stored;
        using (var storeStream = new MemoryStream(bytes, writable: false))
        {
            stored = await _blobStorage.StoreAsync(storeStream, fileName, BlobCategory.GameNightPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
        }

        if (!stored.Success || stored.FilePath is null)
            throw new ValidationException(stored.ErrorMessage ?? "Failed to store the photo.");

        // Thumbnail — best-effort.
        string? thumbUrl = null;
        try
        {
            using var thumbSource = new MemoryStream(bytes, writable: false);
            using var thumb = await ImageThumbnailHelper.GenerateThumbnailAsync(thumbSource, cancellationToken).ConfigureAwait(false);
            if (thumb != null)
            {
                var thumbFileName = $"{photoId:N}_thumb.jpg";
                var thumbResult = await _blobStorage.StoreAsync(thumb, thumbFileName, BlobCategory.GameNightPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
                if (thumbResult.Success) thumbUrl = thumbResult.FilePath;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Thumbnail generation failed for game-night photo {PhotoId}", photoId);
        }

        // OCR — opt-in, best-effort (smoldocling).
        string? ocrText = null;
        double? ocrConfidence = null;
        if (command.ExtractScoreFromPhoto)
        {
            try
            {
                var ocr = await _photoPreprocessor.PreprocessAsync(bytes, cancellationToken).ConfigureAwait(false);
                ocrText = ocr.ExtractedText;
                ocrConfidence = ocr.ConfidenceScore;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "OCR failed for game-night photo {PhotoId}", photoId);
            }
        }

        var entity = new GameNightPhotoEntity
        {
            Id = photoId,
            GameNightId = night.Id,
            BlobUrl = stored.FilePath,
            ThumbnailUrl = thumbUrl,
            FileSizeBytes = command.FileSizeBytes,
            Sha256Hash = sha,
            OcrText = ocrText,
            OcrConfidence = ocrConfidence,
            Caption = command.Caption,
            UploadedByUserId = command.UserId,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        await _photoRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Concurrent identical upload won the (GameNightId, Sha256Hash) unique-index race
            // between our GetBySha256 check and this SaveChanges → treat as idempotent dedup
            // rather than a 500 (lesson #2700). Return the row the winner persisted.
            // Our own store already happened (store-then-save), so best-effort delete our now-orphaned
            // blobs and log them, mirroring the generic-exception path.
            _logger.LogWarning(ex,
                "Concurrent duplicate game-night photo upload for night {GameNightId} sha {Sha}; reclaiming orphaned blobs: photo={PhotoPath} thumb={ThumbPath}",
                command.GameNightId, sha, stored.FilePath, thumbUrl);
            await TryDeleteBlobAsync(stored.FilePath, cancellationToken).ConfigureAwait(false);
            if (thumbUrl is not null)
                await TryDeleteBlobAsync(thumbUrl, cancellationToken).ConfigureAwait(false);

            var winner = await _photoRepository.GetBySha256Async(command.GameNightId, sha, cancellationToken).ConfigureAwait(false);
            if (winner != null)
            {
                var winUrl = await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, winner.BlobUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
                var winThumb = winner.ThumbnailUrl is null
                    ? null
                    : await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, winner.ThumbnailUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
                return new GameNightPhotoUploadResult(winner.Id, winUrl, winThumb, winner.OcrText, WasDeduplicated: true);
            }
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Surface orphaned blobs so they are reclaimable (store-then-save trade-off).
            _logger.LogError(ex,
                "Failed to persist game-night photo {PhotoId}; orphaned blobs may remain: photo={PhotoPath} thumb={ThumbPath}",
                photoId, stored.FilePath, thumbUrl);
            throw;
        }

        var url = await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, stored.FilePath, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
        var thumbPresigned = thumbUrl is null
            ? null
            : await GameNightPhotoUrlResolver.ResolveAsync(_blobStorage, thumbUrl, GameNightPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
        return new GameNightPhotoUploadResult(photoId, url, thumbPresigned, ocrText, WasDeduplicated: false);
    }

    /// <summary>Best-effort blob delete parsing fileId/folder from the stored path (orphan is harmless if it fails).</summary>
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
