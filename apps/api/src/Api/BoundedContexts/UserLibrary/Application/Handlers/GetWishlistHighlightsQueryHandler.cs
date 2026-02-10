using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting top 5 high-priority wishlist items.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class GetWishlistHighlightsQueryHandler : IQueryHandler<GetWishlistHighlightsQuery, IReadOnlyList<WishlistItemDto>>
{
    private readonly IWishlistRepository _wishlistRepository;

    public GetWishlistHighlightsQueryHandler(IWishlistRepository wishlistRepository)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
    }

    public async Task<IReadOnlyList<WishlistItemDto>> Handle(GetWishlistHighlightsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _wishlistRepository.GetHighlightsAsync(query.UserId, count: 5, cancellationToken)
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
