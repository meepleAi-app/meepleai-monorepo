using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for setting RAG public access on a SharedGame.
/// </summary>
internal sealed class SetRagPublicAccessCommandHandler : ICommandHandler<SetRagPublicAccessCommand>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<SetRagPublicAccessCommandHandler> _logger;

    public SetRagPublicAccessCommandHandler(
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<SetRagPublicAccessCommandHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
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
        // durata della cache, e finche' la scrittura non arrivava al database non si vedeva. I due passi
        // (wrapper e `HybridCache` nativo) e il perche' di ciascuno stanno in ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailAndListAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Set RAG public access for SharedGame {SharedGameId} to {IsRagPublic}",
            command.SharedGameId, command.IsRagPublic);
    }
}
