using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing FAQ in a shared game.
/// </summary>
internal sealed class UpdateGameFaqCommandHandler : ICommandHandler<UpdateGameFaqCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateGameFaqCommandHandler> _logger;

    public UpdateGameFaqCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateGameFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UpdateGameFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating FAQ: {FaqId}, Question: {Question}, Order: {Order}",
            command.FaqId, command.Question, command.Order);

        var game = await _repository.GetGameByFaqIdAsync(command.FaqId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Game containing FAQ with ID {command.FaqId} not found");

        game.UpdateFaq(command.FaqId, command.Question, command.Answer, command.Order);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FAQ updated successfully: {FaqId}", command.FaqId);

        return Unit.Value;
    }
}
