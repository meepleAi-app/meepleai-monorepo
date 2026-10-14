using Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Handler for soft-deleting a private game.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <remarks>
/// #4244: il gioco resta nel database come riga cancellata, il suo contenuto no. Chat, PDF e
/// indice vengono tolti per sempre, e PRIMA di segnare il gioco come cancellato: se la pulizia
/// fallisce il gioco resta dov'è e chi cancella lo vede, invece di ritrovarsi un gioco sparito
/// col contenuto ancora nel database. La pulizia si può ripetere.
/// </remarks>
internal sealed class DeletePrivateGameCommandHandler : ICommandHandler<DeletePrivateGameCommand>
{
    private readonly IPrivateGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<DeletePrivateGameCommandHandler> _logger;

    public DeletePrivateGameCommandHandler(
        IPrivateGameRepository repository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<DeletePrivateGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeletePrivateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Retrieve the private game
        var privateGame = await _repository.GetByIdAsync(command.PrivateGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game with ID {command.PrivateGameId} not found");

        // Verify ownership
        if (privateGame.OwnerId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete private game {GameId} owned by {OwnerId}",
                command.UserId,
                command.PrivateGameId,
                privateGame.OwnerId);

            throw new ForbiddenException("You can only delete your own private games");
        }

        // #4244: prima il contenuto (KnowledgeBase lo possiede), poi il gioco.
        await _mediator.Send(new PurgePrivateGameContentCommand(privateGame.Id), cancellationToken)
            .ConfigureAwait(false);

        // Soft delete the game
        privateGame.Delete();

        await _repository.UpdateAsync(privateGame, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted private game {GameId} for user {UserId}: {Title}",
            command.PrivateGameId,
            command.UserId,
            privateGame.Title);
    }
}
