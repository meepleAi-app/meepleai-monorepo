using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Handler for removing a user-custom cover image (L3).
/// Issue #1824. Flow: validate gameId∈library -> verify cover exists ->
/// best-effort R2 delete -> null DB field.
/// </summary>
internal sealed class RemoveCustomCoverCommandHandler : ICommandHandler<RemoveCustomCoverCommand>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveCustomCoverCommandHandler> _logger;

    public RemoveCustomCoverCommandHandler(
        IUserLibraryRepository libraryRepository,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        ILogger<RemoveCustomCoverCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Centralizes the .webp extension to prevent hardcoding it in multiple places.
    /// </summary>
    private static string ToObjectKey(string resourceKey) => $"{resourceKey}.webp";

    public async Task Handle(RemoveCustomCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AC-R9: gameId ∈ user library → 403 if missing
        var entry = await _libraryRepository
            .GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException($"Game {command.GameId} is not in user library");

        if (string.IsNullOrWhiteSpace(entry.CustomCoverR2Key))
        {
            throw new NotFoundException("No custom cover exists for this game");
        }

        var keyToDelete = entry.CustomCoverR2Key;

        // Best-effort R2 cleanup
        try
        {
            await _blobStorage.DeleteAsync(
                ToObjectKey(keyToDelete),
                BlobCategory.GameImage,
                keyToDelete,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to delete custom cover R2 key {Key}; DB will still be nulled", keyToDelete);
        }

        // Null DB field regardless of R2 outcome
        entry.SetCustomCoverR2Key(null);
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Custom cover removed for game {GameId}, user {UserId}",
            command.GameId, command.UserId);
    }
}
