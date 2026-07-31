using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
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
    // Custom cover physical keys end in ".webp" (CoverKind.User suffix); the stored
    // object's Content-Type must match so the CDN serves it correctly.
    private const string WebpContentType = "image/webp";

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

        // #3384 D5-A: the single CoverKeyBuilder owns the ".webp" suffix convention
        // (suffix-free DbKey persisted, PhysicalKey written) so writer and resolver
        // never drift.
        var coverKey = CoverKeyBuilder.ForUser(command.UserId, command.GameId);

        // Best-effort delete old cover so stale R2 objects don't accumulate. Uses the
        // RAW-KEY delete (mirrors CoverUrlResolver's raw-key reads): the categorized
        // DeleteAsync runs PathSecurity.ValidateIdentifier which rejects the '/' in the
        // deterministic key.
        if (entry.HasCustomCover)
        {
            try
            {
                await _blobStorage.DeleteRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.User, entry.CustomCoverR2Key!),
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

        // #3384: write to the EXACT deterministic physical key via the RAW-KEY path.
        // The categorized StoreAsync rejects the slash-containing key via
        // PathSecurity.ValidateIdentifier AND generates a random-id key the resolver
        // cannot reconstruct — either way the cover would never resolve. StoreRawKeyAsync
        // returns false on a write failure (or in local-storage mode, which does not host
        // raw keys); surface it instead of persisting an unresolvable key.
        var stored = await _blobStorage.StoreRawKeyAsync(
            coverKey.PhysicalKey,
            command.FileStream,
            WebpContentType,
            cancellationToken
        ).ConfigureAwait(false);

        if (!stored)
        {
            throw new ExternalServiceException(
                "Impossibile salvare la cover personalizzata: storage oggetti non disponibile.");
        }

        // Persist the suffix-free DB key ONLY after the object is confirmed written.
        entry.SetCustomCoverR2Key(coverKey.DbKey);
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Custom cover uploaded for game {GameId}, user {UserId}, key {Key}",
            command.GameId, command.UserId, coverKey.DbKey);

        // Return presigned URL valid for 1 hour (3600 seconds) for the exact object just written.
        var presignedUrl = await _blobStorage
            .GetPresignedUrlForRawKeyAsync(coverKey.PhysicalKey, expirySeconds: 3600)
            .ConfigureAwait(false) ?? string.Empty;

        return new CustomCoverUploadResult(coverKey.DbKey, presignedUrl);
    }
}
