using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for removing a game from user's library.
/// </summary>
internal class RemoveGameFromLibraryCommandHandler : ICommandHandler<RemoveGameFromLibraryCommand>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveGameFromLibraryCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RemoveGameFromLibraryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find the library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("Game is not in your library");

        // Prepare for removal (raises domain event)
        entry.PrepareForRemoval();

        await _libraryRepository.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
