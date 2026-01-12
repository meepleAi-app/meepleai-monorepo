using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for deleting an existing FAQ from a shared game.
/// </summary>
internal sealed class DeleteGameFaqCommandHandler : ICommandHandler<DeleteGameFaqCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteGameFaqCommandHandler> _logger;

    public DeleteGameFaqCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteGameFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteGameFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Deleting FAQ: {FaqId}", command.FaqId);

        var game = await _repository.GetGameByFaqIdAsync(command.FaqId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Game containing FAQ with ID {command.FaqId} not found");

        game.RemoveFaq(command.FaqId);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FAQ deleted successfully: {FaqId}", command.FaqId);

        return Unit.Value;
    }
}
