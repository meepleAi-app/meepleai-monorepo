using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Helpers;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles uploading a photo to a PlayRecord. Flow: creator-guard → buffer →
/// magic-byte validate → SHA256 dedup → StoreAsync → thumbnail (best-effort) →
/// OCR (opt-in, best-effort) → AddPhoto → UpdateAsync → SaveChanges → presign.
/// Issue #2436 PR-B (ADR-067).
/// </summary>
internal sealed class UploadPlayRecordPhotoCommandHandler
    : ICommandHandler<UploadPlayRecordPhotoCommand, PlayRecordPhotoUploadResult>
{
    private readonly IPlayRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorage;
    private readonly PlayRecordPermissionChecker _permissionChecker;
    private readonly IPhotoPreprocessor _photoPreprocessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UploadPlayRecordPhotoCommandHandler> _logger;

    public UploadPlayRecordPhotoCommandHandler(
        IPlayRecordRepository repository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorage,
        PlayRecordPermissionChecker permissionChecker,
        IPhotoPreprocessor photoPreprocessor,
        TimeProvider timeProvider,
        ILogger<UploadPlayRecordPhotoCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _photoPreprocessor = photoPreprocessor ?? throw new ArgumentNullException(nameof(photoPreprocessor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlayRecordPhotoUploadResult> Handle(UploadPlayRecordPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _repository.GetByIdAsync(command.RecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenException("You do not have permission to add photos to this play record.");

        // Buffer the upload (needed for magic-byte, hash, thumbnail, OCR + re-reads).
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await command.FileStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        using (var validateStream = new MemoryStream(bytes, writable: false))
        {
            var ok = await Api.BoundedContexts.GameManagement.Application.Validators.ImageFileValidator
                .ValidateMagicBytesAsync(validateStream, command.MimeType).ConfigureAwait(false);
            if (!ok)
                throw new ValidationException("File content does not match its declared type.");
        }

        var sha = CryptographyHelper.ComputeSha256Hash(bytes);

        // Idempotent re-upload: same (record, sha) → return existing.
        var existing = record.Photos.FirstOrDefault(p => string.Equals(p.Sha256Hash, sha, StringComparison.Ordinal));
        if (existing != null)
        {
            var existingUrl = await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, existing.BlobUrl, PlayRecordPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
            var existingThumb = existing.ThumbnailUrl is null
                ? null
                : await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, existing.ThumbnailUrl, PlayRecordPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
            return new PlayRecordPhotoUploadResult(existing.Id, existingUrl, existingThumb, existing.OcrText, WasDeduplicated: true);
        }

        var photoId = Guid.NewGuid();
        var ext = string.Equals(command.MimeType, "image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                : string.Equals(command.MimeType, "image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";

        // Use underscore separator so ParseBlobPath can extract fileId (substring before first '_').
        var resourceKey = $"play-record-{record.Id:N}";
        var fileName = $"{photoId:N}_{sha[..8]}{ext}";

        BlobStorageResult stored;
        using (var storeStream = new MemoryStream(bytes, writable: false))
        {
            stored = await _blobStorage.StoreAsync(storeStream, fileName, BlobCategory.PlayRecordPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
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
                var thumbResult = await _blobStorage.StoreAsync(thumb, thumbFileName, BlobCategory.PlayRecordPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
                if (thumbResult.Success) thumbUrl = thumbResult.FilePath;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Thumbnail generation failed for photo {PhotoId}", photoId);
        }

        // OCR — opt-in, best-effort (DEC-3 smoldocling).
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
                _logger.LogWarning(ex, "OCR failed for photo {PhotoId}", photoId);
            }
        }

        // The ≤10-photo cap is enforced on the in-memory aggregate. Concurrent upload races that
        // both pass the cap check are blocked at SaveChanges via PlayRecord's xmin
        // optimistic-concurrency token (ADR-060, #2436 PR-B / #2437-1): the losing write sees a
        // stale xmin → DbUpdateConcurrencyException → global middleware → 409 concurrent-edit.
        record.AddPhoto(photoId, stored.FilePath, thumbUrl, command.FileSizeBytes, sha, ocrText, ocrConfidence, command.Caption, command.UserId, _timeProvider);
        try
        {
            await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // I2: surface orphaned blobs so they are reclaimable (store-then-save trade-off,
            // consistent with SessionAttachmentService). Re-throw to the caller unchanged.
            _logger.LogError(ex,
                "Failed to persist play record photo {PhotoId}; orphaned blobs may remain: photo={PhotoPath} thumb={ThumbPath}",
                photoId, stored.FilePath, thumbUrl);
            throw;
        }

        var url = await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, stored.FilePath, PlayRecordPhotoUrlResolver.DefaultExpirySeconds).ConfigureAwait(false);
        return new PlayRecordPhotoUploadResult(photoId, url, thumbUrl, ocrText, WasDeduplicated: false);
    }
}
