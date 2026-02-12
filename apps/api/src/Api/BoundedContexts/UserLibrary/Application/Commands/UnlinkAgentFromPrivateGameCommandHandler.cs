using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for unlinking an AI agent from a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class UnlinkAgentFromPrivateGameCommandHandler : ICommandHandler<UnlinkAgentFromPrivateGameCommand, Unit>
{
    private readonly IPrivateGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnlinkAgentFromPrivateGameCommandHandler> _logger;

    public UnlinkAgentFromPrivateGameCommandHandler(
        IPrivateGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UnlinkAgentFromPrivateGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UnlinkAgentFromPrivateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Unlinking agent from private game {GameId} for user {UserId}",
            command.GameId, command.UserId);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PrivateGame", command.GameId.ToString());

        // Verify ownership
        if (game.OwnerId != command.UserId)
            throw new UnauthorizedAccessException($"User {command.UserId} is not the owner of game {command.GameId}");

        // Call domain method (validates agent is linked and raises event)
        game.UnlinkAgent();

        await _repository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent unlinked successfully from private game {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
