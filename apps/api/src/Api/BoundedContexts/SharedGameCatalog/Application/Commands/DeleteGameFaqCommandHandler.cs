using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for deleting an existing FAQ from a shared game.
/// </summary>
internal sealed class DeleteGameFaqCommandHandler : ICommandHandler<DeleteGameFaqCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly HybridCache _hybridCache;
    private readonly ILogger<DeleteGameFaqCommandHandler> _logger;

    public DeleteGameFaqCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache,
        ILogger<DeleteGameFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteGameFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Deleting FAQ: {FaqId}", command.FaqId);

        var game = await _repository.GetGameByFaqIdAsync(command.FaqId, cancellationToken).ConfigureAwait(false);

        // #4557: una FAQ di un altro gioco non esiste sotto la rotta di questo.
        if (game is null || game.Id != command.SharedGameId)
        {
            throw new NotFoundException("Faq", command.FaqId.ToString());
        }

        game.RemoveFaq(command.FaqId);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4565: il dettaglio del gioco (`GET /api/v1/shared-games/{id}`) riporta le FAQ e sta in cache
        // sotto `shared-game:{id}` (durata: `DetailCacheL1Expiration` e `DetailCacheL2Expiration` di
        // `GetSharedGameByIdQueryHandler`): senza questa evizione la modifica si vedeva alla scadenza.
        // Dopo il salvataggio, mai prima. Vedi SharedGameDetailCacheInvalidation.
        await SharedGameDetailCacheInvalidation
            .EvictAsync(_cache, _retryPolicy, _hybridCache, command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("FAQ deleted successfully: {FaqId}", command.FaqId);

        return Unit.Value;
    }
}
