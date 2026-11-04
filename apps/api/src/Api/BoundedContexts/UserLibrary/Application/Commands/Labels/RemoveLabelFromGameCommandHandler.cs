using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Handler for removing a label from a game in the user's library.
/// </summary>
internal class RemoveLabelFromGameCommandHandler : ICommandHandler<RemoveLabelFromGameCommand, bool>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;

    public RemoveLabelFromGameCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        HybridCache cache)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<bool> Handle(RemoveLabelFromGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get the library entry with labels
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Game is not in your library");

        // Remove the label (throws if not found)
        entry.RemoveLabel(command.LabelId);

        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4505: il dettaglio del gioco porta le etichette e resta in cache 5 minuti.
        await _cache.RemoveAsync(GetGameDetailQueryHandler.BuildCacheKey(command.UserId, command.GameId), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
