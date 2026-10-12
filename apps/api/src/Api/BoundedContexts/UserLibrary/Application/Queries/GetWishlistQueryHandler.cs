using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting user's full wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class GetWishlistQueryHandler : IQueryHandler<GetWishlistQuery, IReadOnlyList<WishlistItemDto>>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetWishlistQueryHandler(IWishlistRepository wishlistRepository, ISharedGameRepository sharedGameRepository)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<IReadOnlyList<WishlistItemDto>> Handle(GetWishlistQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _wishlistRepository.GetUserWishlistAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await WishlistReadMapper.ToDtosAsync(items, _sharedGameRepository, cancellationToken)
            .ConfigureAwait(false);
    }
}
