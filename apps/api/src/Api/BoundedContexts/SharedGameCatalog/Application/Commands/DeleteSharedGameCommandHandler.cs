using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
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
    private readonly ILogger<DeleteSharedGameCommandHandler> _logger;

    public DeleteSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Deleting shared game: {GameId}, DeletedBy: {UserId}",
            command.GameId, command.DeletedBy);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.GameId} not found");

        // Call domain method (marks as deleted and raises event)
        game.Delete(command.DeletedBy);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game deleted successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
