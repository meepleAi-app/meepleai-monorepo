using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.ImageProcessing;
using Api.Services.Pdf;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for creating a vision snapshot with image upload, preprocessing, and storage.
/// Session Vision AI feature.
/// </summary>
internal sealed class CreateVisionSnapshotCommandHandler
    : IRequestHandler<CreateVisionSnapshotCommand, CreateVisionSnapshotResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IVisionSnapshotRepository _snapshotRepository;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<CreateVisionSnapshotCommandHandler> _logger;

    public CreateVisionSnapshotCommandHandler(
        ISessionRepository sessionRepository,
        IVisionSnapshotRepository snapshotRepository,
        IImagePreprocessor imagePreprocessor,
        IBlobStorageService blobStorageService,
        ILogger<CreateVisionSnapshotCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _snapshotRepository = snapshotRepository;
        _imagePreprocessor = imagePreprocessor;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<CreateVisionSnapshotResult> Handle(
        CreateVisionSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate session exists
        _ = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // 2. Validate tier limits (hardcoded to "alpha" for now)
        var tierConfig = VisionTierLimits.GetConfig("alpha");

        var existingCount = await _snapshotRepository
            .CountBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (existingCount >= tierConfig.MaxSnapshotsPerSession)
        {
            throw new ConflictException(
                $"Maximum snapshots per session reached ({tierConfig.MaxSnapshotsPerSession}).");
        }

        if (request.Images.Count == 0)
        {
            throw new BadRequestException("At least one image is required.");
        }

        if (request.Images.Count > tierConfig.MaxImagesPerSnapshot)
        {
            throw new BadRequestException(
                $"Maximum {tierConfig.MaxImagesPerSnapshot} images per snapshot.");
        }

        // 3. Create snapshot entity
        var snapshot = VisionSnapshot.Create(
            request.SessionId,
            request.UserId,
            request.TurnNumber,
            request.Caption);

        // 4. Process and store each image
        var gameIdForStorage = request.SessionId.ToString("N");

        foreach (var imageUpload in request.Images)
        {
            try
            {
                // Preprocess image (resize, compress)
                var processed = await _imagePreprocessor
                    .ProcessAsync(imageUpload.Data, imageUpload.MediaType)
                    .ConfigureAwait(false);

                // Store in blob storage
                using var stream = new MemoryStream(processed.Data);
                var fileName = imageUpload.FileName ?? $"snapshot_{snapshot.Id:N}_{snapshot.Images.Count}.jpg";

                var storageResult = await _blobStorageService
                    .StoreAsync(stream, fileName, gameIdForStorage, cancellationToken)
                    .ConfigureAwait(false);

                if (!storageResult.Success || storageResult.FileId is null)
                {
                    _logger.LogWarning(
                        "Failed to store vision snapshot image for session {SessionId}: {Error}",
                        request.SessionId, storageResult.ErrorMessage);
                    continue;
                }

                snapshot.AddImage(storageResult.FileId, processed.MediaType, processed.Width, processed.Height);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error processing vision snapshot image for session {SessionId}",
                    request.SessionId);
                // Continue with remaining images — don't fail the whole snapshot
            }
        }

        if (snapshot.Images.Count == 0)
        {
            throw new BadRequestException("No images could be processed successfully.");
        }

        // 5. Persist
        await _snapshotRepository.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await _snapshotRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created vision snapshot {SnapshotId} for session {SessionId} with {ImageCount} images",
            snapshot.Id, request.SessionId, snapshot.Images.Count);

        return new CreateVisionSnapshotResult(snapshot.Id, snapshot.Images.Count);
    }
}
