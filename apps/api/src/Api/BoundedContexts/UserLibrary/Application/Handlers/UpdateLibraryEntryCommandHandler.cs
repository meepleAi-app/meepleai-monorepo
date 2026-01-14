using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for updating a library entry (notes, favorite status).
/// </summary>
internal class UpdateLibraryEntryCommandHandler : ICommandHandler<UpdateLibraryEntryCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLibraryEntryCommandHandler(
        IUserLibraryRepository libraryRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserLibraryEntryDto> Handle(UpdateLibraryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find the library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("Game is not in your library");

        // Get game for DTO
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found");

        // Update notes if provided (null means clear, not provided means keep)
        if (command.Notes != null)
        {
            entry.UpdateNotes(LibraryNotes.FromNullable(command.Notes));
        }

        // Update favorite status if provided
        if (command.IsFavorite.HasValue)
        {
            entry.SetFavorite(command.IsFavorite.Value);
        }

        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserLibraryEntryDto(
            Id: entry.Id,
            UserId: entry.UserId,
            GameId: entry.GameId,
            GameTitle: game.Title.Value,
            GamePublisher: game.Publisher?.Name,
            GameYearPublished: game.YearPublished?.Value,
            GameIconUrl: game.IconUrl,
            GameImageUrl: game.ImageUrl,
            AddedAt: entry.AddedAt,
            Notes: entry.Notes?.Value,
            IsFavorite: entry.IsFavorite
        );
    }
}
