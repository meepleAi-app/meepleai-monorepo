using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update an existing agent typology (admin only).
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal record UpdateAgentTypologyCommand(
    Guid Id,
    string Name,
    string Description,
    string BasePrompt,
    string DefaultStrategyName,
    IDictionary<string, object> DefaultStrategyParameters
) : IRequest<AgentTypologyDto>;
