using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Cleanup R2 blob when a game is removed from user library (L3).
/// Issue #1824. Best-effort cleanup pattern (mirrors PdfDeletedEventHandler from #1873).
/// Failures are logged as warnings and not propagated back to the originating handler
/// (the persistence side is already committed by the time this event fires),
/// except <see cref="OperationCanceledException"/> which is always re-thrown.
/// </summary>
internal sealed class GameRemovedFromLibraryCustomCoverHandler : INotificationHandler<GameRemovedFromLibraryEvent>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<GameRemovedFromLibraryCustomCoverHandler> _logger;

    public GameRemovedFromLibraryCustomCoverHandler(
        IBlobStorageService blobStorage,
        ILogger<GameRemovedFromLibraryCustomCoverHandler> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameRemovedFromLibraryEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.CustomCoverR2Key))
        {
            return; // No cover stored, nothing to clean up
        }

        var resourceKey = notification.CustomCoverR2Key;

        try
        {
            // #3384: raw-key delete (the categorized DeleteAsync rejects the '/' in the
            // deterministic key). Mirrors CoverUrlResolver's raw-key reads.
            await _blobStorage.DeleteRawKeyAsync(
                CoverKeyBuilder.PhysicalKeyFor(CoverKind.User, resourceKey),
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation — caller owns the token
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125    // Sections of code should not be commented out
        // CLEANUP PATTERN: R2 blob cleanup is best-effort.
        // The library entry is already removed from the DB; a blob delete failure
        // here leaves an orphan R2 object that a backfill job can re-evaluate later.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Failed to delete custom cover R2 key {Key} on game-removal event for user {UserId}, game {GameId}; " +
                "orphan blob left in R2 (manual cleanup ops)",
                resourceKey,
                notification.UserId,
                notification.GameId);
        }
    }
}
