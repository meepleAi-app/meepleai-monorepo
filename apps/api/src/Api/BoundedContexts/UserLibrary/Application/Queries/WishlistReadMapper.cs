using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Mappa le voci lette dalla wishlist nel DTO, con il titolo del gioco (#4227).
/// I titoli arrivano dal catalogo condiviso con una sola lettura per tutte le voci: prima la pagina
/// li cercava da sola, con una richiesta di dettaglio per ogni gioco.
/// </summary>
internal static class WishlistReadMapper
{
    public static async Task<IReadOnlyList<WishlistItemDto>> ToDtosAsync(
        IReadOnlyList<WishlistItem> items,
        ISharedGameRepository sharedGameRepository,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return [];

        var names = await sharedGameRepository
            .GetNamesByIdsAsync(items.Select(i => i.GameId).Distinct().ToList(), cancellationToken)
            .ConfigureAwait(false);

        return items
            .Select(item => ToDto(item, names.TryGetValue(item.GameId, out var name) ? name : null))
            .ToList();
    }

    private static WishlistItemDto ToDto(WishlistItem item, string? gameName) => new(
        Id: item.Id,
        UserId: item.UserId,
        GameId: item.GameId,
        Priority: item.Priority.ToString().ToUpperInvariant(),
        TargetPrice: item.TargetPrice,
        Notes: item.Notes,
        AddedAt: item.AddedAt,
        UpdatedAt: item.UpdatedAt,
        Visibility: item.Visibility.ToString().ToUpperInvariant(),
        GameName: gameName);
}
