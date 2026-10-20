using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for deleting an existing erratum from a shared game.
/// NOTE: This implementation uses GetGameByErrataIdAsync from ISharedGameRepository
/// to locate the game containing the erratum for deletion.
/// </summary>
internal sealed class DeleteGameErrataCommandHandler : ICommandHandler<DeleteGameErrataCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly HybridCache _hybridCache;
    private readonly ILogger<DeleteGameErrataCommandHandler> _logger;

    public DeleteGameErrataCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache,
        ILogger<DeleteGameErrataCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteGameErrataCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Deleting erratum: {ErrataId}",
            command.ErrataId);

        // Fetch the game containing this erratum
        var game = await _repository.GetGameByErrataIdAsync(command.ErrataId, cancellationToken).ConfigureAwait(false);

        // #4543: un'errata di un altro gioco non esiste sotto la rotta di questo.
        if (game is null || game.Id != command.SharedGameId)
        {
            throw new NotFoundException("Erratum", command.ErrataId.ToString());
        }

        // Remove the erratum from the game via domain method
        game.RemoveErrata(command.ErrataId);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4565: il dettaglio del gioco (`GET /api/v1/shared-games/{id}`) riporta le errata e sta in cache
        // sotto `shared-game:{id}` (durata: `DetailCacheL1Expiration` e `DetailCacheL2Expiration` di
        // `GetSharedGameByIdQueryHandler`): senza questa evizione la modifica si vedeva alla scadenza.
        // Dopo il salvataggio, mai prima. Vedi SharedGameDetailCacheInvalidation.
        await SharedGameDetailCacheInvalidation
            .EvictAsync(_cache, _retryPolicy, _hybridCache, command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Erratum deleted successfully: {ErrataId}",
            command.ErrataId);

        return Unit.Value;
    }
}
