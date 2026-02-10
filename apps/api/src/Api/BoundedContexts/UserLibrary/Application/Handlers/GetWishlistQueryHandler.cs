using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting user's full wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class GetWishlistQueryHandler : IQueryHandler<GetWishlistQuery, IReadOnlyList<WishlistItemDto>>
{
    private readonly IWishlistRepository _wishlistRepository;

    public GetWishlistQueryHandler(IWishlistRepository wishlistRepository)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
    }

    public async Task<IReadOnlyList<WishlistItemDto>> Handle(GetWishlistQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _wishlistRepository.GetUserWishlistAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return items.Select(MapToDto).ToList();
    }

    private static WishlistItemDto MapToDto(WishlistItem item)
    {
        return new WishlistItemDto(
            Id: item.Id,
            UserId: item.UserId,
            GameId: item.GameId,
            Priority: item.Priority.ToString().ToUpperInvariant(),
            TargetPrice: item.TargetPrice,
            Notes: item.Notes,
            AddedAt: item.AddedAt,
            Visibility: item.Visibility.ToString().ToUpperInvariant()
        );
    }
}
