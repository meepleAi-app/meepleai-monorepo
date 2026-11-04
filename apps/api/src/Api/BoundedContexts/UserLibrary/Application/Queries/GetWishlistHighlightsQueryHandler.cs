using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting top 5 high-priority wishlist items.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class GetWishlistHighlightsQueryHandler : IQueryHandler<GetWishlistHighlightsQuery, IReadOnlyList<WishlistItemDto>>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetWishlistHighlightsQueryHandler(IWishlistRepository wishlistRepository, ISharedGameRepository sharedGameRepository)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<IReadOnlyList<WishlistItemDto>> Handle(GetWishlistHighlightsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _wishlistRepository.GetHighlightsAsync(query.UserId, count: 5, cancellationToken)
            .ConfigureAwait(false);

        return await WishlistReadMapper.ToDtosAsync(items, _sharedGameRepository, cancellationToken)
            .ConfigureAwait(false);
    }
}
