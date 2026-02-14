using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for linking an AI agent to a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class LinkAgentToPrivateGameCommandHandler : ICommandHandler<LinkAgentToPrivateGameCommand, Unit>
{
    private readonly IPrivateGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LinkAgentToPrivateGameCommandHandler> _logger;

    public LinkAgentToPrivateGameCommandHandler(
        IPrivateGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<LinkAgentToPrivateGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(LinkAgentToPrivateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Linking agent {AgentId} to private game {GameId} for user {UserId}",
            command.AgentId, command.GameId, command.UserId);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PrivateGame", command.GameId.ToString());

        // Verify ownership
        if (game.OwnerId != command.UserId)
            throw new UnauthorizedAccessException($"User {command.UserId} is not the owner of game {command.GameId}");

        // Call domain method (validates not already linked and raises event)
        game.LinkAgent(command.AgentId);

        await _repository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent {AgentId} linked successfully to private game {GameId}",
            command.AgentId, command.GameId);

        return Unit.Value;
    }
}
