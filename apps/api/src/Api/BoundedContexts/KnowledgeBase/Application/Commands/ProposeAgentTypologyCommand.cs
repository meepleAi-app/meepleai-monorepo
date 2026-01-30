using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for Editor to propose a new agent typology (creates as Draft).
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal record ProposeAgentTypologyCommand(
    string Name,
    string Description,
    string BasePrompt,
    string DefaultStrategyName,
    IDictionary<string, object> DefaultStrategyParameters,
    Guid ProposedBy
) : IRequest<AgentTypologyDto>;
