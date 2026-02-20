using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for linking an AI agent to a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// Issue #4941: Auto-links indexed PDF documents to the agent after linking.
/// </summary>
internal sealed class LinkAgentToPrivateGameCommandHandler : ICommandHandler<LinkAgentToPrivateGameCommand, Unit>
{
    private readonly IPrivateGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly ILogger<LinkAgentToPrivateGameCommandHandler> _logger;

    public LinkAgentToPrivateGameCommandHandler(
        IPrivateGameRepository repository,
        IUnitOfWork unitOfWork,
        ISender sender,
        ILogger<LinkAgentToPrivateGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
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

        // Guard: an agent is already linked → surface as 409 Conflict (per CLAUDE.md)
        if (game.AgentDefinitionId.HasValue)
            throw new ConflictException($"An agent is already linked to game {command.GameId}.");

        // Call domain method (validates not already linked and raises event)
        game.LinkAgent(command.AgentId);

        await _repository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent {AgentId} linked successfully to private game {GameId}",
            command.AgentId, command.GameId);

        // Issue #4941: Auto-link indexed PDF documents to the newly linked agent (fail-safe).
        // command.AgentId is the AgentDefinitionId — naming follows LinkAgentToPrivateGameCommand convention.
        try
        {
            await _sender
                .Send(new LinkUserAgentDocumentsCommand(command.GameId, command.AgentId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation — do not swallow it.
            throw;
        }
        catch (Exception ex)
        {
            // Fail-safe: document linking is best-effort and must not roll back the agent link.
            _logger.LogWarning(ex,
                "Auto-link of documents to agent {AgentId} for game {GameId} failed. Agent link remains intact.",
                command.AgentId, command.GameId);
        }

        return Unit.Value;
    }
}
