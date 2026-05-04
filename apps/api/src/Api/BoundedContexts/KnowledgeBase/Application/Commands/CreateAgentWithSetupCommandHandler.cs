using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="CreateAgentWithSetupCommand"/>. Orchestrates:
/// <list type="number">
///   <item>Optional <c>AddGameToLibraryCommand</c> when <c>AddToCollection</c> is true
///   (idempotent: <c>"already in"</c> <see cref="DomainException"/> caught and treated
///   as success — orchestration semantics).</item>
///   <item><c>CreateUserAgentCommand</c> (β.2 reuse) — returns the resolved agent DTO.</item>
///   <item>Placeholder <c>ThreadId</c> (chat-thread BC integration deferred) +
///   <c>SlotUsed = 0</c> (tier/quota deferred to Issue #4771).</item>
/// </list>
/// MVP shape sufficient for the frontend AgentCreationSheet setup wizard. Issue #655.
/// </summary>
internal sealed class CreateAgentWithSetupCommandHandler
    : IRequestHandler<CreateAgentWithSetupCommand, CreateAgentWithSetupResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CreateAgentWithSetupCommandHandler> _logger;

    public CreateAgentWithSetupCommandHandler(
        IMediator mediator,
        ILogger<CreateAgentWithSetupCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateAgentWithSetupResult> Handle(
        CreateAgentWithSetupCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: optional library add (idempotent via "already in" DomainException catch).
        var gameAddedToCollection = false;
        if (request.AddToCollection)
        {
            try
            {
                var addCommand = new AddGameToLibraryCommand(
                    UserId: request.UserId,
                    GameId: request.GameId,
                    Notes: null,
                    IsFavorite: false);
                await _mediator.Send(addCommand, cancellationToken).ConfigureAwait(false);
                gameAddedToCollection = true;
            }
            catch (DomainException ex) when (ex.Message.Contains("already in", StringComparison.OrdinalIgnoreCase))
            {
                // Game already in library — count as success for orchestration semantics.
                gameAddedToCollection = true;
                _logger.LogInformation(
                    ex,
                    "Game {GameId} already in library for user {UserId}; treating as success",
                    request.GameId,
                    request.UserId);
            }
        }

        // Step 2: create the agent (β.2 reuse).
        var createAgentCommand = new CreateUserAgentCommand(
            UserId: request.UserId,
            GameId: request.GameId,
            AgentType: request.AgentType,
            Name: request.AgentName,
            StrategyName: request.StrategyName,
            StrategyParameters: request.StrategyParameters,
            DocumentIds: request.DocumentIds);

        var agentDto = await _mediator.Send(createAgentCommand, cancellationToken).ConfigureAwait(false);

        // Step 3+4: placeholder thread + slot (deferred BC integrations).
        return new CreateAgentWithSetupResult(
            AgentId: agentDto.Id,
            AgentName: agentDto.Name,
            ThreadId: Guid.NewGuid(),
            SlotUsed: 0,
            GameAddedToCollection: gameAddedToCollection);
    }
}
