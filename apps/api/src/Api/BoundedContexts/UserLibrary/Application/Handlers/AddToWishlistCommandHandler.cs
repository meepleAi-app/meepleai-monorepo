using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for adding a game to user's wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class AddToWishlistCommandHandler : ICommandHandler<AddToWishlistCommand, WishlistItemDto>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddToWishlistCommandHandler(
        IWishlistRepository wishlistRepository,
        IUnitOfWork unitOfWork)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WishlistItemDto> Handle(AddToWishlistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check if game is already on wishlist
        var existing = await _wishlistRepository.IsGameOnWishlistAsync(
            command.UserId, command.GameId, cancellationToken).ConfigureAwait(false);

        if (existing)
            throw new ConflictException("Game is already on your wishlist");

        // Parse priority
        var priority = Enum.Parse<WishlistPriority>(command.Priority, ignoreCase: true);

        // Create wishlist item
        var item = WishlistItem.Create(
            userId: command.UserId,
            gameId: command.GameId,
            priority: priority,
            targetPrice: command.TargetPrice,
            notes: command.Notes);

        await _wishlistRepository.AddAsync(item, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(item);
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
            UpdatedAt: item.UpdatedAt,
            Visibility: item.Visibility.ToString().ToUpperInvariant()
        );
    }
}
