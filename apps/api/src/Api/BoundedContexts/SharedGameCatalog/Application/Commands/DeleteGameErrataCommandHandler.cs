using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

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
    private readonly ILogger<DeleteGameErrataCommandHandler> _logger;

    public DeleteGameErrataCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteGameErrataCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
        if (game is null)
        {
            throw new InvalidOperationException($"Erratum with ID {command.ErrataId} not found");
        }

        // Remove the erratum from the game via domain method
        game.RemoveErrata(command.ErrataId);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Erratum deleted successfully: {ErrataId}",
            command.ErrataId);

        return Unit.Value;
    }
}
