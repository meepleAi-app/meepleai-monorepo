using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for adding a new erratum to an existing shared game.
/// </summary>
internal sealed class AddGameErrataCommandHandler : ICommandHandler<AddGameErrataCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<AddGameErrataCommandHandler> _logger;

    public AddGameErrataCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<AddGameErrataCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(AddGameErrataCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Adding erratum to shared game: {SharedGameId}, PageReference: {PageReference}",
            command.SharedGameId, command.PageReference);

        // Fetch the game
        var game = await _repository.GetByIdAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.SharedGameId.ToString());
        }

        // Create the erratum
        var errata = GameErrata.Create(command.SharedGameId, command.Description, command.PageReference, command.PublishedDate);

        // Add erratum to game via domain method
        game.AddErrata(errata);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4565: il dettaglio del gioco (`GET /api/v1/shared-games/{id}`) riporta le errata e sta in cache
        // sotto `shared-game:{id}` (durata: `DetailCacheL1Expiration` e `DetailCacheL2Expiration` di
        // `GetSharedGameByIdQueryHandler`): senza questa evizione la modifica si vedeva alla scadenza.
        // Dopo il salvataggio, mai prima. Vedi ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Erratum added successfully: {ErrataId} to game {SharedGameId}",
            errata.Id, command.SharedGameId);

        return errata.Id;
    }
}
