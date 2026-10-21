using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for approving a delete request for a shared game.
/// Approving will immediately delete the game.
/// </summary>
internal sealed class ApproveDeleteRequestCommandHandler : ICommandHandler<ApproveDeleteRequestCommand, Unit>
{
    private readonly ISharedGameDeleteRequestRepository _deleteRequestRepository;
    private readonly ISharedGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<ApproveDeleteRequestCommandHandler> _logger;

    public ApproveDeleteRequestCommandHandler(
        ISharedGameDeleteRequestRepository deleteRequestRepository,
        ISharedGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<ApproveDeleteRequestCommandHandler> logger)
    {
        _deleteRequestRepository = deleteRequestRepository ?? throw new ArgumentNullException(nameof(deleteRequestRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(ApproveDeleteRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Approving delete request: {RequestId}, ApprovedBy: {UserId}",
            command.RequestId, command.ApprovedBy);

        var deleteRequest = await _deleteRequestRepository.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("DeleteRequest", command.RequestId.ToString());

        // Call domain method to approve
        deleteRequest.Approve(command.ApprovedBy, command.Comment);

        // Fetch the game and delete it
        var game = await _gameRepository.GetByIdAsync(deleteRequest.SharedGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", deleteRequest.SharedGameId.ToString());

        game.Delete(command.ApprovedBy);

        _deleteRequestRepository.Update(deleteRequest);
        _gameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4603: il dettaglio e la lista del catalogo in cache portano lo stato del gioco (la lista, la sua
        // stessa presenza), e l'endpoint pubblico nasconde i non pubblicati DOPO la cache: senza questa
        // evizione il pubblico vedeva lo stato di prima fino alla scadenza della voce. Dopo il salvataggio,
        // mai prima. Vedi ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailAndListAsync(deleteRequest.SharedGameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Delete request approved and game deleted: {RequestId}, {GameId}",
            command.RequestId, deleteRequest.SharedGameId);

        return Unit.Value;
    }
}
