using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for removing a game from user's wishlist.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class RemoveFromWishlistCommandHandler : ICommandHandler<RemoveFromWishlistCommand>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveFromWishlistCommandHandler(
        IWishlistRepository wishlistRepository,
        IUnitOfWork unitOfWork)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RemoveFromWishlistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await _wishlistRepository.GetByIdAsync(command.WishlistItemId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Wishlist item {command.WishlistItemId} not found");

        // Verify ownership
        if (item.UserId != command.UserId)
            throw new ForbiddenException("You do not own this wishlist item");

        await _wishlistRepository.DeleteAsync(item, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
