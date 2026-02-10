using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for updating a wishlist item.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class UpdateWishlistItemCommandHandler : ICommandHandler<UpdateWishlistItemCommand, WishlistItemDto>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWishlistItemCommandHandler(
        IWishlistRepository wishlistRepository,
        IUnitOfWork unitOfWork)
    {
        _wishlistRepository = wishlistRepository ?? throw new ArgumentNullException(nameof(wishlistRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WishlistItemDto> Handle(UpdateWishlistItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await _wishlistRepository.GetByIdAsync(command.WishlistItemId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Wishlist item {command.WishlistItemId} not found");

        // Verify ownership
        if (item.UserId != command.UserId)
            throw new ForbiddenException("You do not own this wishlist item");

        // Parse priority if provided
        WishlistPriority? priority = null;
        if (command.Priority != null)
            priority = Enum.Parse<WishlistPriority>(command.Priority, ignoreCase: true);

        // Apply updates
        item.Update(
            priority: priority,
            targetPrice: command.TargetPrice,
            clearTargetPrice: command.ClearTargetPrice,
            notes: command.Notes,
            clearNotes: command.ClearNotes);

        await _wishlistRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
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
            Visibility: item.Visibility.ToString().ToUpperInvariant()
        );
    }
}
