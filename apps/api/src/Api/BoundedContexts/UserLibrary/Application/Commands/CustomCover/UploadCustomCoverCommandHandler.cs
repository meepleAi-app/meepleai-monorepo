using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Handler for uploading a user-custom cover image (L3).
/// Issue #1824. Flow: validate gameId∈library → delete old R2 if exists →
/// upload new → update DB → return presigned URL.
/// </summary>
internal sealed class UploadCustomCoverCommandHandler : ICommandHandler<UploadCustomCoverCommand, CustomCoverUploadResult>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadCustomCoverCommandHandler> _logger;

    public UploadCustomCoverCommandHandler(
        IUserLibraryRepository libraryRepository,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        ILogger<UploadCustomCoverCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomCoverUploadResult> Handle(UploadCustomCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AC-R9: gameId ∈ user library → 403 if missing
        var entry = await _libraryRepository
            .GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException($"Game {command.GameId} is not in user library");

        var resourceKey = $"user-covers/{command.UserId}/{command.GameId}/cover";
        var objectKey = $"{resourceKey}.webp";

        // Best-effort delete old cover so stale R2 objects don't accumulate
        if (entry.HasCustomCover)
        {
            try
            {
                await _blobStorage.DeleteAsync(
                    $"{entry.CustomCoverR2Key}.webp",
                    BlobCategory.GameImage,
                    entry.CustomCoverR2Key!,
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to delete old custom cover R2 key {Key} during replace flow",
                    entry.CustomCoverR2Key);
            }
        }

        // Upload new cover (StoreAsync derives the final path internally)
        await _blobStorage.StoreAsync(
            command.FileStream,
            objectKey,
            BlobCategory.GameImage,
            resourceKey,
            cancellationToken
        ).ConfigureAwait(false);

        // Persist updated R2 key on domain aggregate
        entry.SetCustomCoverR2Key(resourceKey);
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Custom cover uploaded for game {GameId}, user {UserId}, key {Key}",
            command.GameId, command.UserId, resourceKey);

        // Return presigned URL valid for 1 hour (3600 seconds)
        var presignedUrl = await _blobStorage.GetPresignedDownloadUrlAsync(
            objectKey,
            BlobCategory.GameImage,
            resourceKey,
            expirySeconds: 3600
        ).ConfigureAwait(false) ?? string.Empty;

        return new CustomCoverUploadResult(resourceKey, presignedUrl);
    }
}
