using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="QuickCreateAgentCommand"/>. Issue #659 (Phase δ.1).
/// </summary>
/// <remarks>
/// Orchestrates the fast 1-click "Tutor" onboarding flow:
/// <list type="number">
///   <item>Reuses <c>CreateUserAgentCommand</c> (β.2 / PR #692) with hardcoded
///   <c>AgentType = "Tutor"</c>; <c>Name = null</c> so the inner handler
///   auto-derives <c>"Tutor for {GameName}"</c>; default strategy
///   <c>HybridSearch</c>; no documents linked.</item>
///   <item>Returns a <see cref="QuickCreateAgentResult"/> containing the
///   created agent id, a placeholder <c>ChatThreadId</c> (chat-thread BC
///   integration deferred — same as <c>CreateAgentWithSetupCommandHandler</c>
///   in PR #693), the resolved agent name, and <c>KbCardCount = 0</c>
///   (KB query deferred — separate followup if needed).</item>
/// </list>
/// </remarks>
internal sealed class QuickCreateAgentCommandHandler
    : IRequestHandler<QuickCreateAgentCommand, QuickCreateAgentResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<QuickCreateAgentCommandHandler> _logger;

    public QuickCreateAgentCommandHandler(
        IMediator mediator,
        ILogger<QuickCreateAgentCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QuickCreateAgentResult> Handle(
        QuickCreateAgentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.GameId == Guid.Empty)
            throw new ArgumentException("GameId is required", nameof(request));

        var createCommand = new CreateUserAgentCommand(
            UserId: request.UserId,
            GameId: request.GameId,
            AgentType: "Tutor",
            Name: null,                  // auto-derived "Tutor for {GameName}" inside CreateUserAgentCommandHandler
            StrategyName: null,          // default HybridSearch
            StrategyParameters: null,
            DocumentIds: null);

        var agentDto = await _mediator.Send(createCommand, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Quick-created Tutor agent {AgentId} '{AgentName}' for SharedGame {GameId} (user {UserId})",
            agentDto.Id,
            agentDto.Name,
            request.GameId,
            request.UserId);

        return new QuickCreateAgentResult(
            AgentId: agentDto.Id,
            ChatThreadId: Guid.NewGuid(),    // placeholder — chat-thread BC integration deferred (mirrors PR #693)
            AgentName: agentDto.Name,
            KbCardCount: 0);                  // placeholder — KB query deferred
    }
}
