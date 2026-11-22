using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// The game a share request is about, as its notifications name it (#5078). A NewGameProposal names its private game:
/// its SourceGameId is <see cref="Guid.Empty"/>, so a shared game looked up with it is never found, and the
/// notification was skipped. Every other request names the shared game at SourceGameId.
/// </summary>
internal sealed record ShareRequestNotificationGame(string Title, string? ImageUrl)
{
    public static async Task<ShareRequestNotificationGame?> FindAsync(
        ShareRequest shareRequest,
        ISharedGameRepository sharedGames,
        IPrivateGameRepository privateGames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shareRequest);

        if (shareRequest.SourcePrivateGameId is { } privateGameId)
        {
            var privateGame = await privateGames.GetByIdAsync(privateGameId, cancellationToken).ConfigureAwait(false);

            // No image: a private game's may be a BoardGameGeek address, which the catalog no longer carries (ADR-059).
            return privateGame is null ? null : new ShareRequestNotificationGame(privateGame.Title, ImageUrl: null);
        }

        var sharedGame = await sharedGames.GetByIdAsync(shareRequest.SourceGameId, cancellationToken).ConfigureAwait(false);
        return sharedGame is null ? null : new ShareRequestNotificationGame(sharedGame.Title, sharedGame.ImageUrl);
    }
}
