using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Handler for adding a label to a game in the user's library.
/// </summary>
internal class AddLabelToGameCommandHandler : ICommandHandler<AddLabelToGameCommand, LabelDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IGameLabelRepository _labelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;

    public AddLabelToGameCommandHandler(
        IUserLibraryRepository libraryRepository,
        IGameLabelRepository labelRepository,
        IUnitOfWork unitOfWork,
        HybridCache cache)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<LabelDto> Handle(AddLabelToGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Verify the game is in the user's library
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Game is not in your library");

        // Verify the label exists and is accessible to the user
        var label = await _labelRepository.GetAccessibleLabelAsync(command.UserId, command.LabelId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Label with ID {command.LabelId} not found");

        // Add the label to the entry
        entry.AddLabel(command.LabelId);

        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
        {
            // #4505: un'altra richiesta ha assegnato la stessa etichetta dopo la nostra lettura, quindi
            // il controllo del dominio non l'ha vista. EF inserisce il figlio prima di aggiornare la
            // radice: l'indice unico risponde prima dello xmin, e senza questa traduzione è un 500.
            throw new ConflictException($"Label {command.LabelId} is already assigned to this game");
        }

        // #4505: il dettaglio del gioco porta le etichette e resta in cache 5 minuti.
        await _cache.RemoveAsync(GetGameDetailQueryHandler.BuildCacheKey(command.UserId, command.GameId), cancellationToken)
            .ConfigureAwait(false);

        return new LabelDto(
            Id: label.Id,
            Name: label.Name,
            Color: label.Color,
            IsPredefined: label.IsPredefined,
            CreatedAt: label.CreatedAt
        );
    }
}
