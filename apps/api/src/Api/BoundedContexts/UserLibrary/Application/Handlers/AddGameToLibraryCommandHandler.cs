using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for adding a game to user's library.
/// </summary>
internal class AddGameToLibraryCommandHandler : ICommandHandler<AddGameToLibraryCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddGameToLibraryCommandHandler(
        IUserLibraryRepository libraryRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserLibraryEntryDto> Handle(AddGameToLibraryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate game exists
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found");

        // Check if already in library
        if (await _libraryRepository.IsGameInLibraryAsync(command.UserId, command.GameId, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainException("Game is already in your library");
        }

        // Create library entry
        var entry = new UserLibraryEntry(Guid.NewGuid(), command.UserId, command.GameId);

        // Set notes if provided
        if (!string.IsNullOrWhiteSpace(command.Notes))
        {
            entry.UpdateNotes(LibraryNotes.FromNullable(command.Notes));
        }

        // Set favorite status if requested
        if (command.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        await _libraryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
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
