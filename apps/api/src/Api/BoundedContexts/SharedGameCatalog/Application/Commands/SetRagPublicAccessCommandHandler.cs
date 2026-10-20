using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for setting RAG public access on a SharedGame.
/// </summary>
internal sealed class SetRagPublicAccessCommandHandler : ICommandHandler<SetRagPublicAccessCommand>
{
    // Il tag con cui SearchSharedGamesQueryHandler mette la lista in cache.
    private const string SearchGamesTag = "search-games";

    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly HybridCache _hybridCache;
    private readonly ILogger<SetRagPublicAccessCommandHandler> _logger;

    public SetRagPublicAccessCommandHandler(
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache,
        ILogger<SetRagPublicAccessCommandHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SetRagPublicAccessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _sharedGameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"SharedGame {command.SharedGameId} not found");

        game.SetRagPublicAccess(command.IsRagPublic);
        _sharedGameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4585: la lista del catalogo (`SearchSharedGamesQueryHandler`) mostra `isRagPublic` e vive in
        // HybridCache sotto il tag `search-games`. Senza l'evizione serviva il valore precedente per la
        // durata della cache, e finché la scrittura non arrivava al database non si vedeva.
        //
        // Due passi, perché il primo da solo non basta. L'helper è lo stesso di UpdateSharedGameCommandHandler
        // (#4100): retry policy e Pub/Sub verso le altre repliche. Ma il wrapper rimuove solo le chiavi
        // elencate nel suo set Redis, che si riempie solo per le voci scritte da lui, e la lista è scritta
        // con `HybridCache` grezzo: è il limite di #4561 (vedi UserActivityCacheInvalidationEventHandler,
        // #4559). Il tag nativo raggiunge la voce della lista su questa istanza, anche senza Redis. Quando
        // #4561 renderà il wrapper capace di farlo, il secondo passo si può togliere.
        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _retryPolicy, command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);
        await _hybridCache.RemoveByTagAsync(SearchGamesTag, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Set RAG public access for SharedGame {SharedGameId} to {IsRagPublic}",
            command.SharedGameId, command.IsRagPublic);
    }
}
