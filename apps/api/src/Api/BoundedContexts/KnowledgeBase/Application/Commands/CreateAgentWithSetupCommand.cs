using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for the orchestrated agent creation flow consumed by the frontend
/// <c>agentsClient.createWithSetup</c> wizard helper. Issue #655 (Phase β.3).
/// </summary>
/// <remarks>
/// Orchestration sequence:
/// <list type="number">
///   <item>Optional library add via <c>AddGameToLibraryCommand</c> (idempotent: <c>"already in"</c>
///   DomainException is treated as success).</item>
///   <item>Agent creation via <c>CreateUserAgentCommand</c> (β.2 reuse).</item>
///   <item>Result includes placeholder <c>ThreadId</c> (chat-thread BC integration deferred) and
///   <c>SlotUsed = 0</c> (tier/quota deferred to Issue #4771).</item>
/// </list>
/// </remarks>
internal sealed record CreateAgentWithSetupCommand(
    Guid UserId,
    Guid GameId,
    bool AddToCollection,
    string AgentType,
    string? AgentName = null,
    string? StrategyName = null,
    IReadOnlyDictionary<string, object>? StrategyParameters = null,
    IReadOnlyList<Guid>? DocumentIds = null
) : IRequest<CreateAgentWithSetupResult>;

/// <summary>
/// Result returned by <see cref="CreateAgentWithSetupCommand"/>. Mirrors the frontend
/// <c>CreateAgentWithSetupResponse</c> shape consumed by the AgentCreationSheet wizard.
/// </summary>
internal sealed record CreateAgentWithSetupResult(
    Guid AgentId,
    string AgentName,
    Guid ThreadId,
    int SlotUsed,
    bool GameAddedToCollection
);
