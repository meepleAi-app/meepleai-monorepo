using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to delete an agent definition.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed record DeleteAgentDefinitionCommand(Guid Id) : IRequest;
