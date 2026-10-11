using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// #4155: la dichiarazione di possesso cambia lo stato della voce di libreria (Nuovo → Owned), ma il
/// dettaglio del gioco (<see cref="GetGameDetailQueryHandler"/>) resta in cache 5 minuti e nessuno
/// lo invalidava, mentre il cambio di stato esplicito (<c>UpdateGameStateCommandHandler</c>) lo fa.
/// </summary>
/// <remarks>
/// Un handler dell'evento e non una riga nel command handler: l'evento parte da
/// <c>UserLibraryEntry.DeclareOwnership</c>, quindi copre qualunque percorso dichiari il possesso.
/// </remarks>
internal sealed class GameDetailCacheInvalidationHandler : INotificationHandler<OwnershipDeclaredEvent>
{
    private readonly HybridCache _cache;
    private readonly ILogger<GameDetailCacheInvalidationHandler> _logger;

    public GameDetailCacheInvalidationHandler(HybridCache cache, ILogger<GameDetailCacheInvalidationHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(OwnershipDeclaredEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await _cache
            .RemoveAsync(GetGameDetailQueryHandler.BuildCacheKey(notification.UserId, notification.GameId), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "Ownership declared for game {GameId} by user {UserId}: game detail cache evicted",
            notification.GameId, notification.UserId);
    }
}
