using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for deleting a shared game (soft delete).
/// </summary>
internal sealed class DeleteSharedGameCommandHandler : ICommandHandler<DeleteSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<DeleteSharedGameCommandHandler> _logger;

    public DeleteSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<DeleteSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Deleting shared game: {GameId}, DeletedBy: {UserId}",
            command.GameId, command.DeletedBy);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        // Call domain method (marks as deleted and raises event)
        game.Delete(command.DeletedBy);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4603: il dettaglio e la lista del catalogo in cache portano lo stato del gioco (la lista, la sua
        // stessa presenza), e l'endpoint pubblico nasconde i non pubblicati DOPO la cache: senza questa
        // evizione il pubblico vedeva lo stato di prima fino alla scadenza della voce. Dopo il salvataggio,
        // mai prima. Vedi ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailAndListAsync(command.GameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game deleted successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
